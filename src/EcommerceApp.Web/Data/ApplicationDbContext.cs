using EcommerceApp.Web.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
    public DbSet<DiscountRedemption> DiscountRedemptions => Set<DiscountRedemption>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Category>(e => { e.HasIndex(x => x.Slug).IsUnique(); e.Property(x => x.Name).HasMaxLength(80); e.Property(x => x.Slug).HasMaxLength(80); });
        b.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique(); e.HasIndex(x => x.Sku).IsUnique(); e.HasIndex(x => new { x.IsActive, x.CategoryId, x.Price });
            e.Property(x => x.Price).HasPrecision(18, 2); e.Property(x => x.StockVersion).IsConcurrencyToken();
            e.ToTable(t => { t.HasCheckConstraint("CK_Product_Price", "\"Price\" >= 0"); t.HasCheckConstraint("CK_Product_Stock", "\"StockQuantity\" >= 0"); });
            e.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Address>().HasOne(x => x.User).WithMany(x => x.Addresses).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Cart>(e => { e.HasIndex(x => x.SessionKey).IsUnique(); e.HasIndex(x => x.UserId).IsUnique(); e.Property(x => x.DiscountCode).HasMaxLength(64); e.HasMany(x => x.Items).WithOne(x => x.Cart).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<CartItem>(e => { e.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique(); e.ToTable(t => t.HasCheckConstraint("CK_CartItem_Quantity", "\"Quantity\" > 0")); });
        b.Entity<DiscountCode>(e => { e.HasIndex(x => x.NormalizedCode).IsUnique(); e.Property(x => x.NormalizedCode).HasMaxLength(64); e.Property(x => x.Value).HasPrecision(18, 2); e.Property(x => x.MinimumSubtotal).HasPrecision(18, 2); e.Property(x => x.MaximumDiscount).HasPrecision(18, 2); e.ToTable(t => { t.HasCheckConstraint("CK_DiscountCode_Value", "\"Value\" > 0"); t.HasCheckConstraint("CK_DiscountCode_Window", "\"EndsAt\" > \"StartsAt\""); t.HasCheckConstraint("CK_DiscountCode_UsageLimit", "\"UsageLimit\" IS NULL OR \"UsageLimit\" > 0"); }); });
        b.Entity<Order>(e => { e.HasIndex(x => x.OrderNumber).IsUnique(); e.HasIndex(x => x.CheckoutToken).IsUnique(); e.HasIndex(x => x.ConfirmationToken).IsUnique(); e.HasIndex(x => new { x.CustomerId, x.CreatedAt }); e.HasIndex(x => new { x.Status, x.CreatedAt }); e.Property(x => x.Subtotal).HasPrecision(18, 2); e.Property(x => x.DiscountTotal).HasPrecision(18, 2); e.Property(x => x.ShippingTotal).HasPrecision(18, 2); e.Property(x => x.Total).HasPrecision(18, 2); });
        b.Entity<OrderItem>(e => { e.Property(x => x.UnitPrice).HasPrecision(18, 2); e.ToTable(t => t.HasCheckConstraint("CK_OrderItem_Quantity", "\"Quantity\" > 0")); e.HasOne(x => x.Product).WithMany().OnDelete(DeleteBehavior.SetNull); });
        b.Entity<DiscountRedemption>().HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
