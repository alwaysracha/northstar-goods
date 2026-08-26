using System.ComponentModel.DataAnnotations;
using EcommerceApp.Web.Services.Cart;

namespace EcommerceApp.Web.ViewModels;

public sealed class CheckoutModel
{
    [Required, EmailAddress] public string ContactEmail { get; set; } = "";
    [Required, StringLength(100)] public string RecipientName { get; set; } = "";
    [Required, StringLength(150)] public string Line1 { get; set; } = "";
    [StringLength(150)] public string? Line2 { get; set; }
    [Required, StringLength(80)] public string City { get; set; } = "";
    [Required, StringLength(80)] public string Region { get; set; } = "";
    [Required, StringLength(20)] public string PostalCode { get; set; } = "";
    [Required, StringLength(2, MinimumLength = 2)] public string CountryCode { get; set; } = "US";
    [Required] public string PaymentMethod { get; set; } = "simulated";
    [Required] public string CheckoutToken { get; set; } = "";
    public CartSummary? Cart { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping => Cart is not null && Cart.Subtotal - Discount >= 100 ? 0 : 8;
    public decimal Total => (Cart?.Subtotal ?? 0) - Discount + Shipping;
}
