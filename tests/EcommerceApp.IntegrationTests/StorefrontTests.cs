using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class StorefrontTests(IntegrationTestFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_is_public_and_healthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Home_and_catalog_render_seeded_content()
    {
        var home = await _client.GetStringAsync("/");
        var catalog = await _client.GetStringAsync("/catalog?search=lamp&sort=price-asc");
        Assert.Contains("Thoughtful goods", home);
        Assert.Contains("products found", catalog);
    }

    [Fact]
    public async Task Catalog_sort_and_pagination_preserve_all_filter_state()
    {
        var html = await _client.GetStringAsync("/catalog?minPrice=20&maxPrice=200&inStockOnly=true&sort=price-desc&pageSize=3");
        Assert.Contains("<input type=\"hidden\" name=\"minPrice\" value=\"20\"", html);
        Assert.Contains("<input type=\"hidden\" name=\"maxPrice\" value=\"200\"", html);
        Assert.Contains("<input type=\"hidden\" name=\"inStockOnly\" value=\"true\"", html);
        Assert.Contains("minPrice=20", html);
        Assert.Contains("maxPrice=200", html);
        Assert.Contains("inStockOnly=True", html);
        Assert.Contains("pageSize=3", html);
    }

    [Fact]
    public async Task Unknown_product_returns_404()
    {
        var response = await _client.GetAsync("/products/not-a-real-product");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/account/register", "Create your account")]
    [InlineData("/account/login", "Welcome back")]
    [InlineData("/cart", "Your bag")]
    public async Task Milestone_two_pages_are_public_and_branded(string path, string expected)
    {
        var html = await _client.GetStringAsync(path);
        Assert.Contains(expected, html);
    }

    [Fact]
    public async Task Mutations_reject_missing_antiforgery_token()
    {
        var response = await _client.PostAsync("/cart/add", new FormUrlEncodedContent(new Dictionary<string, string> { ["productId"] = "1", ["quantity"] = "1" }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task External_return_url_is_not_reflected_into_account_form()
    {
        var html = await _client.GetStringAsync("/account/login?returnUrl=https%3A%2F%2Fevil.example");
        Assert.DoesNotContain("evil.example", html);
    }

    [Fact]
    public async Task Development_seed_is_complete_idempotent_and_role_correct()
    {
        using var scope = factory.Services.CreateScope();
        await DevelopmentDataSeeder.SeedAsync(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        string[] seededCategorySlugs = ["audio", "home", "kitchen", "workspace", "outdoors", "wellness", "travel", "accessories"];
        Assert.Equal(8, await db.Categories.CountAsync(x => seededCategorySlugs.Contains(x.Slug)));
        Assert.Equal(50, await db.Products.CountAsync(x => x.Sku.StartsWith("NST-")));
        Assert.Equal(4, await db.Users.CountAsync());
        Assert.Equal(3, await db.Addresses.CountAsync());
        Assert.Equal(3, await db.Orders.CountAsync(x => x.OrderNumber.StartsWith("NST-2026-")));
        var admin = await users.FindByEmailAsync(DevelopmentDataSeeder.AdminEmail);
        Assert.NotNull(admin);
        Assert.True(await users.IsInRoleAsync(admin, "Administrator"));
    }
}
