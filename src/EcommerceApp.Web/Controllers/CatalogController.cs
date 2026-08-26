using EcommerceApp.Web.Services.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceApp.Web.Controllers;

[Route("catalog")]
public sealed class CatalogController(ICatalogService catalog) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] CatalogQuery query, CancellationToken ct)
    {
        ViewBag.Categories = await catalog.CategoriesAsync(ct);
        return View(await catalog.SearchAsync(query, ct));
    }

    [HttpGet("/products/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        var product = await catalog.GetBySlugAsync(slug, ct);
        return product is null ? NotFound() : View(product);
    }
}
