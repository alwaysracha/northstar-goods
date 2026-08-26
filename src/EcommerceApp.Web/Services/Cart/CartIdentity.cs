using System.Security.Claims;

namespace EcommerceApp.Web.Services.Cart;

public interface ICartIdentity { string Key { get; } string? UserId { get; } }
public sealed class CartIdentity(IHttpContextAccessor accessor) : ICartIdentity
{
    private const string CookieName = "northstar.cart";
    public string? UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
    public string Key
    {
        get
        {
            var context = accessor.HttpContext ?? throw new InvalidOperationException("No HTTP context.");
            if (context.Request.Cookies.TryGetValue(CookieName, out var value) && Guid.TryParseExact(value, "N", out _)) return value;
            value = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append(CookieName, value, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true, Secure = context.Request.IsHttps, MaxAge = TimeSpan.FromDays(30) });
            return value;
        }
    }
}
