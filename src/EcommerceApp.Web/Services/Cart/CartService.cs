using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Services.Cart;

public sealed record CartLine(int ProductId, string Name, string Slug, string ImagePath, decimal UnitPrice, int Quantity, int Stock, bool Available)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
public sealed record CartSummary(Guid? Id, IReadOnlyList<CartLine> Items, string? DiscountCode)
{
    public decimal Subtotal => Items.Sum(x => x.LineTotal);
    public int Count => Items.Sum(x => x.Quantity);
}

public interface ICartService
{
    Task<CartSummary> GetAsync(string sessionKey, string? userId, CancellationToken ct = default);
    Task AddAsync(string sessionKey, string? userId, int productId, int quantity, CancellationToken ct = default);
    Task UpdateAsync(string sessionKey, string? userId, int productId, int quantity, CancellationToken ct = default);
    Task RemoveAsync(string sessionKey, string? userId, int productId, CancellationToken ct = default);
    Task SetDiscountAsync(string sessionKey, string? userId, string? code, CancellationToken ct = default);
    Task MergeAsync(string sessionKey, string userId, CancellationToken ct = default);
}

public sealed class CartService(ApplicationDbContext db) : ICartService
{
    public async Task<CartSummary> GetAsync(string sessionKey, string? userId, CancellationToken ct = default)
    {
        var cart = await Query(sessionKey, userId).AsNoTracking().SingleOrDefaultAsync(ct);
        return cart is null ? new(null, [], null) : ToSummary(cart);
    }

    public async Task AddAsync(string sessionKey, string? userId, int productId, int quantity, CancellationToken ct = default)
    {
        if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));
        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId && x.IsActive, ct) ?? throw new InvalidOperationException("Product is unavailable.");
        if (product.StockQuantity < 1) throw new InvalidOperationException("Product is out of stock.");
        var cart = await FindOrCreate(sessionKey, userId, ct);
        var item = cart.Items.SingleOrDefault(x => x.ProductId == productId);
        if (item is null) cart.Items.Add(new CartItem { ProductId = productId, Quantity = Math.Min(quantity, product.StockQuantity) });
        else item.Quantity = Math.Min(item.Quantity + quantity, product.StockQuantity);
        cart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(string sessionKey, string? userId, int productId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0) { await RemoveAsync(sessionKey, userId, productId, ct); return; }
        var cart = await FindOrCreate(sessionKey, userId, ct);
        var item = cart.Items.SingleOrDefault(x => x.ProductId == productId) ?? throw new InvalidOperationException("Cart item was not found.");
        var product = await db.Products.SingleAsync(x => x.Id == productId, ct);
        if (!product.IsActive || product.StockQuantity < 1) throw new InvalidOperationException("Product is unavailable.");
        item.Quantity = Math.Min(quantity, product.StockQuantity); cart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(string sessionKey, string? userId, int productId, CancellationToken ct = default)
    {
        var cart = await Query(sessionKey, userId).SingleOrDefaultAsync(ct);
        var item = cart?.Items.SingleOrDefault(x => x.ProductId == productId);
        if (item is not null) { db.CartItems.Remove(item); cart!.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); }
    }

    public async Task SetDiscountAsync(string sessionKey, string? userId, string? code, CancellationToken ct = default)
    { var cart = await FindOrCreate(sessionKey, userId, ct); cart.DiscountCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim(); await db.SaveChangesAsync(ct); }

    public async Task MergeAsync(string sessionKey, string userId, CancellationToken ct = default)
    {
        var guest = await db.Carts.Include(x => x.Items).ThenInclude(x => x.Product).SingleOrDefaultAsync(x => x.SessionKey == sessionKey && x.UserId == null, ct);
        var owned = await db.Carts.Include(x => x.Items).ThenInclude(x => x.Product).SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (guest is null) return;
        if (owned is null) { guest.UserId = userId; guest.SessionKey = null; await db.SaveChangesAsync(ct); return; }
        foreach (var source in guest.Items) { if (!source.Product.IsActive || source.Product.StockQuantity < 1) continue; var target = owned.Items.SingleOrDefault(x => x.ProductId == source.ProductId); if (target is null) owned.Items.Add(new CartItem { ProductId = source.ProductId, Quantity = Math.Min(source.Quantity, source.Product.StockQuantity) }); else target.Quantity = Math.Min(target.Quantity + source.Quantity, source.Product.StockQuantity); }
        owned.DiscountCode ??= guest.DiscountCode; db.Carts.Remove(guest); await db.SaveChangesAsync(ct);
    }

    private IQueryable<Domain.Entities.Cart> Query(string key, string? userId) => db.Carts.Include(x => x.Items).ThenInclude(x => x.Product).Where(x => userId != null ? x.UserId == userId : x.SessionKey == key && x.UserId == null);
    private async Task<Domain.Entities.Cart> FindOrCreate(string key, string? userId, CancellationToken ct)
    { var cart = await Query(key, userId).SingleOrDefaultAsync(ct); if (cart is not null) return cart; cart = new Domain.Entities.Cart { UserId = userId, SessionKey = userId is null ? key : null }; db.Carts.Add(cart); return cart; }
    private static CartSummary ToSummary(Domain.Entities.Cart c) => new(c.Id, c.Items.Select(x => new CartLine(x.ProductId, x.Product.Name, x.Product.Slug, x.Product.ImagePath, x.Product.Price, x.Quantity, x.Product.StockQuantity, x.Product.IsActive)).ToList(), c.DiscountCode);
}
