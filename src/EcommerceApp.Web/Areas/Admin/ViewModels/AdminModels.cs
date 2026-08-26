using System.ComponentModel.DataAnnotations;
using EcommerceApp.Web.Domain.Entities;

namespace EcommerceApp.Web.Areas.Admin.ViewModels;

public sealed record DashboardModel(int Categories, int Products, int ActiveProducts, int LowStockProducts, int Customers, int Orders, decimal Revenue);

public sealed class CategoryEditModel
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; } = "";
    [Required, StringLength(80), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, numbers, and hyphens.")] public string Slug { get; set; } = "";
    [StringLength(500)] public string Description { get; set; } = "";
    [Required] public string ImagePath { get; set; } = "/images/categories/accessories.svg";
    [Range(0, 1000)] public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProductEditModel
{
    public int Id { get; set; }
    public long StockVersion { get; set; }
    [Required] public int CategoryId { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Required, StringLength(120), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")] public string Slug { get; set; } = "";
    [Required, StringLength(40)] public string Sku { get; set; } = "";
    [StringLength(240)] public string ShortDescription { get; set; } = "";
    [StringLength(4000)] public string Description { get; set; } = "";
    [Range(typeof(decimal), "0", "999999.99")] public decimal Price { get; set; }
    [Range(0, 1000000)] public int StockQuantity { get; set; }
    [Required] public string ImagePath { get; set; } = "/images/products/accessories.svg";
    public string? SecondaryImagePath { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class DiscountEditModel : IValidatableObject
{
    public int Id { get; set; }
    [Required, StringLength(64), RegularExpression("^[A-Za-z0-9_-]+$")] public string Code { get; set; } = "";
    public DiscountKind Kind { get; set; }
    [Range(typeof(decimal), "0.01", "999999.99")] public decimal Value { get; set; }
    [Range(typeof(decimal), "0", "999999.99")] public decimal MinimumSubtotal { get; set; }
    [Range(typeof(decimal), "0.01", "999999.99")] public decimal? MaximumDiscount { get; set; }
    [Required] public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow;
    [Required] public DateTimeOffset EndsAt { get; set; } = DateTimeOffset.UtcNow.AddMonths(1);
    public bool IsActive { get; set; } = true;
    [Range(1, int.MaxValue)] public int? UsageLimit { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndsAt <= StartsAt) yield return new("End time must be after start time.", [nameof(EndsAt)]);
        if (Kind == DiscountKind.Percentage && Value > 100) yield return new("Percentage cannot exceed 100.", [nameof(Value)]);
    }
}

public sealed class OrderStatusEditModel
{
    [Required] public OrderStatus Status { get; set; }
    [Required] public OrderStatus OriginalStatus { get; set; }
}
