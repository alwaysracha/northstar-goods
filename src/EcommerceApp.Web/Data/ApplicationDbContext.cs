using EcommerceApp.Web.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Data;

// Maps onto the hand-written SQL Server schema in /database. The T-SQL scripts are the source of truth for
// tables, constraints, views and procedures; this model only needs names, keys and types to line up
// (DatabaseSchemaTests checks every mapped column exists). There are no EF migrations.
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
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<ApplicationUser>(e => { e.ToTable("Users", "auth"); e.Property(x => x.DisplayName).HasMaxLength(100); e.Property(x => x.PhoneNumber).HasMaxLength(32); });
        b.Entity<IdentityRole>().ToTable("Roles", "auth");
        b.Entity<IdentityUserRole<string>>().ToTable("UserRoles", "auth");
        b.Entity<IdentityUserClaim<string>>().ToTable("UserClaims", "auth");
        b.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims", "auth");
        b.Entity<IdentityUserLogin<string>>().ToTable("UserLogins", "auth");
        b.Entity<IdentityUserToken<string>>().ToTable("UserTokens", "auth");

        b.Entity<Category>(e => { e.ToTable("Categories", "catalog"); e.HasIndex(x => x.Slug).IsUnique(); e.Property(x => x.Name).HasMaxLength(80); e.Property(x => x.Slug).HasMaxLength(80).IsUnicode(false); });
        b.Entity<Product>(e =>
        {
            e.ToTable("Products", "catalog");
            e.HasIndex(x => x.Slug).IsUnique(); e.HasIndex(x => x.Sku).IsUnique(); e.HasIndex(x => new { x.IsActive, x.CategoryId, x.Price });
            e.Property(x => x.Slug).HasMaxLength(120).IsUnicode(false); e.Property(x => x.Sku).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.Price).HasPrecision(18, 2); e.Property(x => x.StockVersion).IsConcurrencyToken();
            e.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Address>(e => { e.ToTable("Addresses", "customer"); e.Property(x => x.CountryCode).HasMaxLength(2).IsFixedLength().IsUnicode(false); e.HasOne(x => x.User).WithMany(x => x.Addresses).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });

        b.Entity<Cart>(e => { e.ToTable("Carts", "sales"); e.HasIndex(x => x.SessionKey).IsUnique(); e.HasIndex(x => x.UserId).IsUnique(); e.Property(x => x.SessionKey).HasMaxLength(64).IsUnicode(false); e.Property(x => x.DiscountCode).HasMaxLength(64); e.HasMany(x => x.Items).WithOne(x => x.Cart).HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade); });
        b.Entity<CartItem>(e => { e.ToTable("CartItems", "sales"); e.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique(); });
        b.Entity<DiscountCode>(e =>
        {
            e.ToTable("DiscountCodes", "sales");
            e.HasIndex(x => x.NormalizedCode).IsUnique(); e.Property(x => x.NormalizedCode).HasMaxLength(64);
            e.Property(x => x.Kind).HasColumnName("DiscountKindId").HasConversion<byte>();
            e.Property(x => x.Value).HasPrecision(18, 2); e.Property(x => x.MinimumSubtotal).HasPrecision(18, 2); e.Property(x => x.MaximumDiscount).HasPrecision(18, 2);
        });
        b.Entity<DiscountRedemption>(e =>
        {
            e.ToTable("DiscountRedemptions", "sales");
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DiscountCode).WithMany(x => x.Redemptions).HasForeignKey(x => x.DiscountCodeId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Order>(e =>
        {
            e.ToTable("Orders", "sales");
            e.HasIndex(x => x.OrderNumber).IsUnique(); e.HasIndex(x => x.CheckoutToken).IsUnique(); e.HasIndex(x => x.ConfirmationToken).IsUnique();
            e.HasIndex(x => new { x.CustomerId, x.CreatedAt }); e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.Property(x => x.OrderNumber).HasMaxLength(64).IsUnicode(false); e.Property(x => x.CheckoutToken).HasMaxLength(128).IsUnicode(false); e.Property(x => x.ConfirmationToken).HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.ShipCountryCode).HasMaxLength(2).IsFixedLength().IsUnicode(false);
            e.Property(x => x.Status).HasColumnName("OrderStatusId").HasConversion<byte>();
            e.Property(x => x.PaymentStatus).HasColumnName("PaymentStatusId").HasConversion<byte>();
            e.Property(x => x.Subtotal).HasPrecision(18, 2); e.Property(x => x.DiscountTotal).HasPrecision(18, 2); e.Property(x => x.ShippingTotal).HasPrecision(18, 2); e.Property(x => x.Total).HasPrecision(18, 2);
            e.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.StatusHistory).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.PaymentAttempts).WithOne(x => x.Order).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OrderItem>(e => { e.ToTable("OrderItems", "sales"); e.Property(x => x.Sku).HasMaxLength(40).IsUnicode(false); e.Property(x => x.UnitPrice).HasPrecision(18, 2); e.HasOne(x => x.Product).WithMany().OnDelete(DeleteBehavior.SetNull); });
        b.Entity<OrderStatusHistory>(e =>
        {
            e.ToTable("OrderStatusHistory", "sales");
            e.Property(x => x.FromStatus).HasColumnName("FromStatusId").HasConversion<byte>();
            e.Property(x => x.ToStatus).HasColumnName("ToStatusId").HasConversion<byte>();
            e.Property(x => x.ReasonId).HasColumnName("StatusChangeReasonId");
            e.Property(x => x.Note).HasMaxLength(500);
        });
        b.Entity<PaymentAttempt>(e =>
        {
            e.ToTable("PaymentAttempts", "payment");
            e.Property(x => x.Status).HasColumnName("PaymentAttemptStatusId").HasConversion<byte>();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsUnicode(false);
            e.Property(x => x.GatewayReference).HasMaxLength(64).IsUnicode(false);
        });
    }
}
