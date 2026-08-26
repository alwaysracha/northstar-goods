using EcommerceApp.Web.Areas.Admin.ViewModels;
using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Areas.Admin.Controllers;

public sealed class ProductsController(ApplicationDbContext db) : AdminControllerBase
{
    public static readonly string[] Images = ["/images/products/accessories.svg", "/images/products/audio.svg", "/images/products/home.svg", "/images/products/kitchen.svg", "/images/products/outdoors.svg", "/images/products/travel.svg", "/images/products/wellness.svg", "/images/products/workspace.svg"];

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var query = db.Products.AsNoTracking().Include(x => x.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.Sku.Contains(term));
        }
        ViewBag.Search = search;
        return View(await query.OrderBy(x => x.Name).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await Populate();
        return View("Edit", new ProductEditModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Create(ProductEditModel model) => Save(model, false);

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        await Populate();
        return View(new ProductEditModel
        {
            Id = product.Id,
            StockVersion = product.StockVersion,
            CategoryId = product.CategoryId,
            Name = product.Name,
            Slug = product.Slug,
            Sku = product.Sku,
            ShortDescription = product.ShortDescription,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            ImagePath = product.ImagePath,
            SecondaryImagePath = product.SecondaryImagePath,
            IsFeatured = product.IsFeatured,
            IsActive = product.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(int id, ProductEditModel model)
    {
        if (id != model.Id) return Task.FromResult<IActionResult>(NotFound());
        return Save(model, true);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        if (await db.OrderItems.AnyAsync(i => i.ProductId == id) || await db.CartItems.AnyAsync(i => i.ProductId == id))
        {
            product.IsActive = false;
            product.IsFeatured = false;
            await db.SaveChangesAsync();
            TempData["Error"] = "Product is referenced by commerce records and was deactivated instead of deleted.";
        }
        else
        {
            db.Remove(product);
            await db.SaveChangesAsync();
            TempData["Notice"] = "Product deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> Save(ProductEditModel model, bool editing)
    {
        if (!ModelState.IsValid)
        {
            await Populate();
            return View("Edit", model);
        }

        model.Name = model.Name.Trim();
        model.Slug = model.Slug.Trim().ToLowerInvariant();
        model.Sku = model.Sku.Trim().ToUpperInvariant();
        model.ShortDescription = model.ShortDescription.Trim();
        model.Description = model.Description.Trim();
        model.SecondaryImagePath = string.IsNullOrWhiteSpace(model.SecondaryImagePath) ? null : model.SecondaryImagePath;

        if (!Images.Contains(model.ImagePath)) ModelState.AddModelError(nameof(model.ImagePath), "Select an approved local image.");
        if (model.SecondaryImagePath is not null && !CategoriesController.Images.Contains(model.SecondaryImagePath) && !Images.Contains(model.SecondaryImagePath)) ModelState.AddModelError(nameof(model.SecondaryImagePath), "Select an approved local image.");
        if (!await db.Categories.AnyAsync(x => x.Id == model.CategoryId)) ModelState.AddModelError(nameof(model.CategoryId), "Select an existing category.");
        if (await db.Products.AnyAsync(x => x.Slug == model.Slug && x.Id != model.Id)) ModelState.AddModelError(nameof(model.Slug), "This slug is already in use.");
        if (await db.Products.AnyAsync(x => x.Sku == model.Sku && x.Id != model.Id)) ModelState.AddModelError(nameof(model.Sku), "This SKU is already in use.");
        if (!ModelState.IsValid)
        {
            await Populate();
            return View("Edit", model);
        }

        Product product;
        if (editing)
        {
            product = await db.Products.FindAsync(model.Id) ?? throw new InvalidOperationException();
            db.Entry(product).Property(x => x.StockVersion).OriginalValue = model.StockVersion;
        }
        else
        {
            product = new Product { Name = model.Name, Slug = model.Slug, Sku = model.Sku };
            db.Add(product);
        }

        product.CategoryId = model.CategoryId;
        product.Name = model.Name;
        product.Slug = model.Slug;
        product.Sku = model.Sku;
        product.ShortDescription = model.ShortDescription;
        product.Description = model.Description;
        product.Price = model.Price;
        product.StockQuantity = model.StockQuantity;
        product.ImagePath = model.ImagePath;
        product.SecondaryImagePath = model.SecondaryImagePath;
        product.IsFeatured = model.IsFeatured;
        product.IsActive = model.IsActive;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        product.StockVersion++;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException) when (editing)
        {
            db.ChangeTracker.Clear();
            var current = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == model.Id);
            if (current is null) return NotFound();
            model.StockQuantity = current.StockQuantity;
            model.StockVersion = current.StockVersion;
            ModelState.AddModelError("", "This product changed after you opened it. Current stock has been reloaded; review and submit again.");
            Response.StatusCode = StatusCodes.Status409Conflict;
            await Populate();
            return View("Edit", model);
        }

        TempData["Notice"] = $"Product {(editing ? "updated" : "created")}.";
        return RedirectToAction(nameof(Index));
    }

    private async Task Populate()
    {
        ViewBag.Categories = await db.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        ViewBag.Images = Images;
        ViewBag.SecondaryImages = Images.Concat(CategoriesController.Images).ToArray();
    }
}
