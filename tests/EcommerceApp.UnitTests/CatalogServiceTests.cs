using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using EcommerceApp.Web.Services.Catalog;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.UnitTests;

public sealed class CatalogServiceTests
{
    private static async Task<(ApplicationDbContext Db, CatalogService Service)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ApplicationDbContext(options);
        var category = new Category { Id = 1, Name = "Audio", Slug = "audio", IsActive = true };
        db.AddRange(category,
            new Product { Id = 1, Category = category, CategoryId = 1, Name = "Studio Headphones", Slug = "studio-headphones", Sku = "AUD-001", Price = 149.00m, StockQuantity = 9, IsActive = true, IsFeatured = true },
            new Product { Id = 2, Category = category, CategoryId = 1, Name = "Pocket Speaker", Slug = "pocket-speaker", Sku = "AUD-002", Price = 59.00m, StockQuantity = 0, IsActive = true },
            new Product { Id = 3, Category = category, CategoryId = 1, Name = "Hidden", Slug = "hidden", Sku = "AUD-003", Price = 1m, StockQuantity = 2, IsActive = false });
        await db.SaveChangesAsync();
        return (db, new CatalogService(db));
    }

    [Fact]
    public async Task Search_filters_inactive_and_matches_name()
    {
        var (_, service) = await CreateAsync();
        var result = await service.SearchAsync(new CatalogQuery { Search = "studio" });
        Assert.Single(result.Items);
        Assert.Equal("Studio Headphones", result.Items[0].Name);
    }

    [Fact]
    public async Task Filters_sort_and_pagination_are_applied()
    {
        var (_, service) = await CreateAsync();
        var result = await service.SearchAsync(new CatalogQuery { InStockOnly = true, Sort = "price-desc", Page = 9, PageSize = 1 });
        Assert.Equal(1, result.Page);
        Assert.Single(result.Items);
        Assert.Equal(149.00m, result.Items[0].Price);
    }

    [Fact]
    public async Task Unknown_or_inactive_product_returns_null()
    {
        var (_, service) = await CreateAsync();
        Assert.Null(await service.GetBySlugAsync("missing"));
        Assert.Null(await service.GetBySlugAsync("hidden"));
    }
}
