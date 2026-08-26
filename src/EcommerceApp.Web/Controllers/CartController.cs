using EcommerceApp.Web.Services.Cart;
using EcommerceApp.Web.Services.Discounts;
using EcommerceApp.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Web.Controllers;

public sealed class CartController(ICartService carts, ICartIdentity identity, IDiscountService discounts) : Controller
{
    [HttpGet] public async Task<IActionResult> Index() => View(await Page());
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Add(int productId, int quantity = 1, string? returnUrl = null) { try { await carts.AddAsync(identity.Key, identity.UserId, productId, quantity); TempData["Notice"] = "Added to your bag."; } catch (InvalidOperationException e) { TempData["Error"] = e.Message; } return LocalRedirect(Safe(returnUrl) ?? Url.Action(nameof(Index))!); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Update(int productId, int quantity) { try { await carts.UpdateAsync(identity.Key, identity.UserId, productId, quantity); } catch (Exception e) when (e is InvalidOperationException or ArgumentOutOfRangeException) { TempData["Error"] = e.Message; } return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Remove(int productId) { await carts.RemoveAsync(identity.Key, identity.UserId, productId); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> ApplyDiscount(string? code) { var cart = await carts.GetAsync(identity.Key, identity.UserId); var (_, result) = await discounts.EvaluateAsync(code, cart.Subtotal); if (result.IsValid) await carts.SetDiscountAsync(identity.Key, identity.UserId, code); TempData[result.IsValid ? "Notice" : "Error"] = result.Message; return RedirectToAction(nameof(Index)); }
    private async Task<CartPage> Page() { var cart = await carts.GetAsync(identity.Key, identity.UserId); if (cart.DiscountCode is null) return new(cart, 0, null); var (_, result) = await discounts.EvaluateAsync(cart.DiscountCode, cart.Subtotal); return new(cart, result.IsValid ? result.Amount : 0, result.Message); }
    private string? Safe(string? value) => !string.IsNullOrWhiteSpace(value) && Url.IsLocalUrl(value) ? value : null;
}
