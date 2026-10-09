using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using EcommerceApp.Web.Services.Catalog;
using EcommerceApp.Web.Services.Cart;
using EcommerceApp.Web.Services.Checkout;
using EcommerceApp.Web.Services.Discounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connection))
{
    if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required outside Development.");
    connection = "Server=localhost,1433;Database=NorthstarGoods;User Id=northstar_app;Password=LocalDev!App2026;Encrypt=True;TrustServerCertificate=True";
}
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 10;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
}).AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options => { options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax; options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always; options.LoginPath = "/account/login"; options.AccessDeniedPath = "/account/access-denied"; options.SlidingExpiration = true; });
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<ICartIdentity, CartIdentity>();
builder.Services.AddScoped<IDiscountService, DiscountService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapAreaControllerRoute("admin", "Admin", "admin/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("product", "products/{slug}", new { controller = "Catalog", action = "Detail" });
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

// The schema and development data are deployed by database/deploy.sh (T-SQL), not by the application.
app.Run();

public partial class Program;
