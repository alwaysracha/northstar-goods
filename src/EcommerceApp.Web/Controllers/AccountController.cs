using EcommerceApp.Web.Domain.Entities;
using EcommerceApp.Web.Services.Cart;
using EcommerceApp.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Web.Controllers;

public sealed class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, ICartService carts, ICartIdentity cartIdentity) : Controller
{
    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ModelState.Remove(nameof(returnUrl));
        return View(new RegisterModel { ReturnUrl = Safe(returnUrl) });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterModel model)
    {
        model.ReturnUrl = Safe(model.ReturnUrl);
        ModelState.Remove(nameof(model.ReturnUrl));
        if (!ModelState.IsValid) return View(model);
        var user = new ApplicationUser { UserName = model.Email.Trim(), Email = model.Email.Trim(), DisplayName = model.DisplayName.Trim(), LockoutEnabled = true };
        var result = await users.CreateAsync(user, model.Password);
        if (!result.Succeeded) { foreach (var e in result.Errors) ModelState.AddModelError("", e.Description); return View(model); }
        await carts.MergeAsync(cartIdentity.Key, user.Id); await signIn.SignInAsync(user, false);
        return LocalRedirect(model.ReturnUrl ?? "/");
    }
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ModelState.Remove(nameof(returnUrl));
        return View(new LoginModel { ReturnUrl = Safe(returnUrl) });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginModel model)
    {
        model.ReturnUrl = Safe(model.ReturnUrl);
        ModelState.Remove(nameof(model.ReturnUrl));
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user is null) { ModelState.AddModelError("", "Email or password is incorrect."); return View(model); }
        var result = await signIn.CheckPasswordSignInAsync(user, model.Password, true);
        if (!result.Succeeded) { ModelState.AddModelError("", result.IsLockedOut ? "Account temporarily locked." : "Email or password is incorrect."); return View(model); }
        await carts.MergeAsync(cartIdentity.Key, user.Id); await signIn.SignInAsync(user, model.RememberMe);
        return LocalRedirect(model.ReturnUrl ?? "/");
    }
    [Authorize, HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return RedirectToAction("Index", "Home"); }
    public IActionResult AccessDenied() { Response.StatusCode = StatusCodes.Status403Forbidden; return View(); }
    private string? Safe(string? value) => !string.IsNullOrWhiteSpace(value) && Url.IsLocalUrl(value) ? value : null;
}
