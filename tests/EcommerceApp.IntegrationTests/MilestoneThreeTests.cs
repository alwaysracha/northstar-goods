using System.Net;
using System.Text.RegularExpressions;
using EcommerceApp.Web.Areas.Admin.Controllers;
using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class MilestoneThreeTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Anonymous_customer_history_redirects_to_login_and_admin_does_too()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Customer_is_forbidden_from_admin_and_cannot_read_another_customers_order()
    {
        using var client = await LoginAsync("leo@example.local");
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/admin")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var foreignOrder = await db.Orders.AsNoTracking().SingleAsync(x => x.OrderNumber == "NST-2026-0002");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/orders/{foreignOrder.OrderNumber}")).StatusCode);
    }

    [Fact]
    public async Task Owner_sees_history_and_detail_while_guest_confirmation_remains_secure()
    {
        using var client = await LoginAsync("maya@example.local");
        var list = await client.GetStringAsync("/orders");
        Assert.Contains("Your orders", list);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var own = await db.Orders.AsNoTracking().SingleAsync(x => x.OrderNumber == "NST-2026-0002");
        Assert.Contains(own.OrderNumber, await client.GetStringAsync($"/orders/{own.OrderNumber}"));
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/checkout/confirmation?token=not-a-token")).StatusCode);
        var confirmation = await anonymous.GetAsync($"/checkout/confirmation?token={own.ConfirmationToken}");
        var confirmationHtml = await confirmation.Content.ReadAsStringAsync();
        Assert.Contains(own.OrderNumber, confirmationHtml);
        Assert.Contains("href=\"/catalog\"", confirmationHtml);
        Assert.Contains("href=\"/#our-story\"", confirmationHtml);
        Assert.Contains("no-store", confirmation.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Admin_succeeds_and_mutations_require_antiforgery()
    {
        using var client = await LoginAsync(DemoAccounts.AdminEmail);
        Assert.Contains("Admin dashboard", await client.GetStringAsync("/admin"));
        var response = await client.PostAsync("/admin/categories/create", new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "Unique category", ["Slug"] = "unique-category" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_category_slug_is_validation_error_and_referenced_category_delete_is_safe()
    {
        using var client = await LoginAsync(DemoAccounts.AdminEmail);
        var createHtml = await client.GetStringAsync("/admin/categories/create");
        var create = await client.PostAsync("/admin/categories/create", Form(createHtml, new() { ["Name"] = "Duplicate audio", ["Slug"] = "audio", ["Description"] = "Duplicate" }));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.Contains("already in use", await create.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var audioId = await db.Categories.Where(x => x.Slug == "audio").Select(x => x.Id).SingleAsync();
        var indexHtml = await client.GetStringAsync("/admin/categories");
        var delete = await client.PostAsync($"/admin/categories/delete/{audioId}", Form(indexHtml));
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
        Assert.True(await db.Categories.AnyAsync(x => x.Id == audioId));
    }

    [Fact]
    public async Task Stale_product_edit_cannot_restore_stock_sold_after_form_was_loaded()
    {
        using var client = await LoginAsync(DemoAccounts.AdminEmail);
        int productId;
        int categoryId;
        string html;
        await using (var setup = factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            categoryId = await db.Categories.Select(x => x.Id).FirstAsync();
            var product = new Product { CategoryId = categoryId, Name = "Concurrency product", Slug = "concurrency-" + Guid.NewGuid().ToString("N"), Sku = "CON-" + Guid.NewGuid().ToString("N"), Price = 20, StockQuantity = 5, ImagePath = ProductsController.Images[0] };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;
        }
        html = await client.GetStringAsync($"/admin/products/edit/{productId}");
        await using (var sale = factory.Services.CreateAsyncScope())
        {
            var db = sale.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Products.Where(x => x.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(x => x.StockQuantity, 4).SetProperty(x => x.StockVersion, x => x.StockVersion + 1));
        }

        var response = await client.PostAsync($"/admin/products/edit/{productId}", Form(html, new()
        {
            ["Id"] = productId.ToString(),
            ["CategoryId"] = categoryId.ToString(),
            ["Name"] = "Concurrency product",
            ["Slug"] = "concurrency-product-edited-" + productId,
            ["Sku"] = "CON-EDIT-" + productId,
            ["ShortDescription"] = "Edited",
            ["Description"] = "Edited description",
            ["Price"] = "20",
            ["StockQuantity"] = "8",
            ["ImagePath"] = ProductsController.Images[0],
            ["IsActive"] = "true",
            ["StockVersion"] = "0"
        }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var verify = factory.Services.CreateAsyncScope();
        Assert.Equal(4, await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Products.Where(x => x.Id == productId).Select(x => x.StockQuantity).SingleAsync());
    }

    [Fact]
    public async Task Production_error_endpoint_returns_safe_500_page()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/Home/Error");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Something went wrong", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_order_status_transition_is_rejected()
    {
        using var client = await LoginAsync(DemoAccounts.AdminEmail);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await db.Orders.AsNoTracking().FirstAsync(x => x.Status == OrderStatus.Shipped);
        var html = await client.GetStringAsync($"/admin/orders/{order.Id}");
        var response = await client.PostAsync($"/admin/orders/{order.Id}/status", Form(html, new() { ["Status"] = OrderStatus.Processing.ToString(), ["OriginalStatus"] = order.Status.ToString() }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        db.ChangeTracker.Clear();
        Assert.Equal(OrderStatus.Shipped, (await db.Orders.FindAsync(order.Id))!.Status);
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var html = await client.GetStringAsync("/account/login");
        var response = await client.PostAsync("/account/login", Form(html, new() { ["Email"] = email, ["Password"] = DemoAccounts.DemoPassword }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static FormUrlEncodedContent Form(string html, Dictionary<string, string>? values = null)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Antiforgery token was not rendered.");
        values ??= [];
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
        return new FormUrlEncodedContent(values);
    }
}
