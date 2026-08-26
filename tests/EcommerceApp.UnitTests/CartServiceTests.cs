using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using EcommerceApp.Web.Services.Cart;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.UnitTests;

public sealed class CartServiceTests
{
    [Fact]
    public async Task Add_uses_authoritative_product_and_caps_stock()
    {
        await using var db = NewDb();
        var category = new Category { Name = "C", Slug = "c" };
        var product = new Product { Category = category, Name = "P", Slug = "p", Sku = "P1", Price = 12, StockQuantity = 2 };
        db.Add(product); await db.SaveChangesAsync();
        var service = new CartService(db);
        await service.AddAsync("guest-a", null, product.Id, 99);
        var cart = await service.GetAsync("guest-a", null);
        Assert.Equal(2, cart.Items.Single().Quantity);
        Assert.Equal(24m, cart.Subtotal);
    }

    [Fact]
    public async Task Separate_keys_have_isolated_carts()
    {
        await using var db = NewDb();
        var product = new Product { Category = new Category { Name = "C", Slug = "c" }, Name = "P", Slug = "p", Sku = "P1", Price = 9, StockQuantity = 5 };
        db.Add(product); await db.SaveChangesAsync();
        var service = new CartService(db);
        await service.AddAsync("one", null, product.Id, 1);
        Assert.Empty((await service.GetAsync("two", null)).Items);
    }

    private static ApplicationDbContext NewDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
