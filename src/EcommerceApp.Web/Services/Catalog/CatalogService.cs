using EcommerceApp.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Services.Catalog;

public sealed class CatalogQuery
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool InStockOnly { get; set; }
    public string Sort { get; set; } = "featured";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
}

public sealed record ProductCard(int Id, string Name, string Slug, string Category, decimal Price, int Stock, string ImagePath, string Summary, bool Featured);
public sealed record CategoryCard(string Name, string Slug, string Description, string ImagePath, int ProductCount);
public sealed record ProductDetail(int Id, string Name, string Slug, string Sku, string Category, decimal Price, int Stock, string ImagePath, string? SecondaryImagePath, string Summary, string Description);
public sealed record PagedCatalog(IReadOnlyList<ProductCard> Items, int Total, int Page, int TotalPages, CatalogQuery Query);

public interface ICatalogService
{
    Task<PagedCatalog> SearchAsync(CatalogQuery query, CancellationToken ct = default);
    Task<ProductDetail?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<ProductCard>> FeaturedAsync(int count, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryCard>> CategoriesAsync(CancellationToken ct = default);
}

public sealed class CatalogService(ApplicationDbContext db) : ICatalogService
{
    public async Task<PagedCatalog> SearchAsync(CatalogQuery request, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking().Where(x => x.IsActive && x.Category.IsActive);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.ShortDescription.ToLower().Contains(term) || x.Sku.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(request.Category)) query = query.Where(x => x.Category.Slug == request.Category);
        if (request.MinPrice is >= 0) query = query.Where(x => x.Price >= request.MinPrice);
        if (request.MaxPrice is >= 0) query = query.Where(x => x.Price <= request.MaxPrice);
        if (request.InStockOnly) query = query.Where(x => x.StockQuantity > 0);
        query = request.Sort switch
        {
            "price-asc" => query.OrderBy(x => x.Price).ThenBy(x => x.Name),
            "price-desc" => query.OrderByDescending(x => x.Price).ThenBy(x => x.Name),
            "name" => query.OrderBy(x => x.Name),
            "newest" => query.OrderByDescending(x => x.CreatedAt),
            _ => query.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.Name)
        };
        var total = await query.CountAsync(ct);
        request.PageSize = Math.Clamp(request.PageSize, 1, 48);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)request.PageSize));
        request.Page = Math.Clamp(request.Page, 1, pages);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new ProductCard(x.Id, x.Name, x.Slug, x.Category.Name, x.Price, x.StockQuantity, x.ImagePath, x.ShortDescription, x.IsFeatured)).ToListAsync(ct);
        return new PagedCatalog(items, total, request.Page, pages, request);
    }

    public Task<ProductDetail?> GetBySlugAsync(string slug, CancellationToken ct = default) => db.Products.AsNoTracking().Where(x => x.IsActive && x.Category.IsActive && x.Slug == slug)
        .Select(x => new ProductDetail(x.Id, x.Name, x.Slug, x.Sku, x.Category.Name, x.Price, x.StockQuantity, x.ImagePath, x.SecondaryImagePath, x.ShortDescription, x.Description)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ProductCard>> FeaturedAsync(int count, CancellationToken ct = default) => await db.Products.AsNoTracking().Where(x => x.IsActive && x.IsFeatured && x.Category.IsActive).OrderBy(x => x.Name).Take(count)
        .Select(x => new ProductCard(x.Id, x.Name, x.Slug, x.Category.Name, x.Price, x.StockQuantity, x.ImagePath, x.ShortDescription, x.IsFeatured)).ToListAsync(ct);

    public async Task<IReadOnlyList<CategoryCard>> CategoriesAsync(CancellationToken ct = default) => await db.Categories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DisplayOrder)
        .Select(x => new CategoryCard(x.Name, x.Slug, x.Description, x.ImagePath, x.Products.Count(p => p.IsActive))).ToListAsync(ct);
}
