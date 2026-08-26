using EcommerceApp.Web.Services.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Web.Controllers;

public sealed class HomeController(ICatalogService catalog) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(new HomeViewModel(await catalog.CategoriesAsync(ct), await catalog.FeaturedAsync(8, ct)));

    [HttpGet]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View();
    }
}
public sealed record HomeViewModel(IReadOnlyList<CategoryCard> Categories, IReadOnlyList<ProductCard> Featured);
