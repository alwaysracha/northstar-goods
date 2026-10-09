using Microsoft.AspNetCore.Identity;

namespace EcommerceApp.Web.Domain.Entities;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Address> Addresses { get; set; } = [];
}

public sealed class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string Description { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Product> Products { get; set; } = [];
}

public sealed class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string Sku { get; set; }
    public string ShortDescription { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string ImagePath { get; set; } = "";
    public string? SecondaryImagePath { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public long StockVersion { get; set; }
}

public sealed class Address
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Label { get; set; } = "Home";
    public required string RecipientName { get; set; }
    public required string Line1 { get; set; }
    public string? Line2 { get; set; }
    public required string City { get; set; }
    public required string Region { get; set; }
    public required string PostalCode { get; set; }
    public string CountryCode { get; set; } = "US";
}

public sealed class Cart
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string? SessionKey { get; set; }
    public string? DiscountCode { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<CartItem> Items { get; set; } = [];
}

public sealed class CartItem
{
    public int Id { get; set; }
    public Guid CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
}

public enum DiscountKind { Percentage, FixedAmount }
public enum OrderStatus { Pending, Processing, Shipped, Delivered, Cancelled }
public enum PaymentStatus { Pending, SimulatedPaid, Failed, Refunded, PartiallyRefunded }
public enum PaymentAttemptStatus : byte { Captured = 1, Declined = 2, Error = 3, Pending = 4 }

// Ids of ref.StatusChangeReasons rows the application writes.
public static class StatusChangeReasons { public const short OrderPlaced = 1, PaymentCaptured = 2, AdminUpdate = 10; }

public sealed class DiscountCode
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public string NormalizedCode { get; set; } = "";
    public DiscountKind Kind { get; set; }
    public decimal Value { get; set; }
    public decimal MinimumSubtotal { get; set; }
    public decimal? MaximumDiscount { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int? UsageLimit { get; set; }
    public ICollection<DiscountRedemption> Redemptions { get; set; } = [];
}

public sealed class DiscountRedemption
{
    public int Id { get; set; }
    public int DiscountCodeId { get; set; }
    public DiscountCode DiscountCode { get; set; } = null!;
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public DateTimeOffset RedeemedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Order
{
    public int Id { get; set; }
    public required string OrderNumber { get; set; }
    public required string CheckoutToken { get; set; }
    public required string ConfirmationToken { get; set; }
    public string? CustomerId { get; set; }
    public ApplicationUser? Customer { get; set; }
    public required string ContactEmail { get; set; }
    public required string RecipientName { get; set; }
    public required string ShipLine1 { get; set; }
    public string? ShipLine2 { get; set; }
    public required string ShipCity { get; set; }
    public required string ShipRegion { get; set; }
    public required string ShipPostalCode { get; set; }
    public required string ShipCountryCode { get; set; }
    public string ShippingAddress => string.Join("\n", new[] { ShipLine1, ShipLine2, $"{ShipCity}, {ShipRegion} {ShipPostalCode}", ShipCountryCode }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal Total { get; set; }
    public string? DiscountCodeSnapshot { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<OrderItem> Items { get; set; } = [];
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<PaymentAttempt> PaymentAttempts { get; set; } = [];
}

public sealed class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public required string Sku { get; set; }
    public required string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public sealed class OrderStatusHistory
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public short? ReasonId { get; set; }
    public string? ChangedByUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Note { get; set; }
}

public sealed class PaymentAttempt
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public byte AttemptNumber { get; set; }
    public int? PaymentMethodId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public PaymentAttemptStatus Status { get; set; }
    public short? DeclineReasonId { get; set; }
    public required string GatewayReference { get; set; }
    public DateTimeOffset AttemptedAt { get; set; } = DateTimeOffset.UtcNow;
}
