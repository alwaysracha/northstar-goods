using EcommerceApp.Web.Data;
using EcommerceApp.Web.Services.Cart;
using EcommerceApp.Web.Services.Checkout;
using EcommerceApp.Web.Services.Discounts;
using EcommerceApp.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Controllers;

public sealed class CheckoutController(ICartService carts, ICartIdentity identity, IDiscountService discounts, ICheckoutService checkout, ApplicationDbContext db, UserManager<Domain.Entities.ApplicationUser> users) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new CheckoutModel { CheckoutToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant() };
        if (identity.UserId is not null) { var user = await users.GetUserAsync(User); model.ContactEmail = user?.Email ?? ""; model.RecipientName = user?.DisplayName ?? ""; var address = await db.Addresses.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == identity.UserId); if (address is not null) { model.RecipientName = address.RecipientName; model.Line1 = address.Line1; model.Line2 = address.Line2; model.City = address.City; model.Region = address.Region; model.PostalCode = address.PostalCode; model.CountryCode = address.CountryCode; } }
        if (!await Populate(model)) return RedirectToAction("Index", "Cart"); return View(model);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutModel model)
    {
        if (!await Populate(model)) { ModelState.AddModelError("", "Your bag is empty."); return View(model); }
        if (!ModelState.IsValid) return View(model);
        var result = await checkout.PlaceAsync(model.Cart!.Id!.Value, identity.UserId, model);
        if (!result.Succeeded) { ModelState.AddModelError("", result.Message); await Populate(model); return View(model); }
        return RedirectToAction(nameof(Confirmation), new { token = result.ConfirmationToken });
    }
    [HttpGet]
    public async Task<IActionResult> Confirmation(string token)
    { Response.Headers.CacheControl = "no-store"; if (string.IsNullOrWhiteSpace(token)) return NotFound(); var order = await db.Orders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.ConfirmationToken == token); return order is null ? NotFound() : View(order); }
    private async Task<bool> Populate(CheckoutModel model) { model.Cart = await carts.GetAsync(identity.Key, identity.UserId); if (model.Cart.Id is null || !model.Cart.Items.Any()) return false; if (model.Cart.DiscountCode is not null) { var (_, r) = await discounts.EvaluateAsync(model.Cart.DiscountCode, model.Cart.Subtotal); model.Discount = r.IsValid ? r.Amount : 0; } return true; }
}
