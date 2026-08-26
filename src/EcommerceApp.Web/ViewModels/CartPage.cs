using EcommerceApp.Web.Services.Cart;
namespace EcommerceApp.Web.ViewModels;

public sealed record CartPage(CartSummary Cart, decimal Discount, string? DiscountMessage) { public decimal Total => Math.Max(0, Cart.Subtotal - Discount); }
