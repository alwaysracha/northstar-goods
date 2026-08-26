using EcommerceApp.Web.Areas.Admin.ViewModels;
using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Areas.Admin.Controllers;

public sealed class CategoriesController(ApplicationDbContext db) : AdminControllerBase
{
    public static readonly string[] Images = ["/images/categories/accessories.svg", "/images/categories/audio.svg", "/images/categories/home.svg", "/images/categories/kitchen.svg", "/images/categories/outdoors.svg", "/images/categories/travel.svg", "/images/categories/wellness.svg", "/images/categories/workspace.svg"];
    [HttpGet] public async Task<IActionResult> Index() => View(await db.Categories.AsNoTracking().Include(x => x.Products).OrderBy(x => x.DisplayOrder).ToListAsync());
    [HttpGet] public IActionResult Create() { Populate(); return View("Edit", new CategoryEditModel()); }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Create(CategoryEditModel model) => Save(model, false);
    [HttpGet] public async Task<IActionResult> Edit(int id) { var x = await db.Categories.FindAsync(id); if (x is null) return NotFound(); Populate(); return View(new CategoryEditModel { Id = x.Id, Name = x.Name, Slug = x.Slug, Description = x.Description, ImagePath = x.ImagePath, DisplayOrder = x.DisplayOrder, IsActive = x.IsActive }); }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Edit(int id, CategoryEditModel model) { if (id != model.Id) return Task.FromResult<IActionResult>(NotFound()); return Save(model, true); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Delete(int id) { var x = await db.Categories.Include(c => c.Products).SingleOrDefaultAsync(c => c.Id == id); if (x is null) return NotFound(); if (x.Products.Count > 0) { TempData["Error"] = "Categories with products cannot be deleted. Deactivate the category instead."; return RedirectToAction(nameof(Index)); } db.Remove(x); await db.SaveChangesAsync(); TempData["Notice"] = "Category deleted."; return RedirectToAction(nameof(Index)); }
    private async Task<IActionResult> Save(CategoryEditModel m, bool editing) { m.Name = m.Name.Trim(); m.Slug = m.Slug.Trim().ToLowerInvariant(); if (!Images.Contains(m.ImagePath)) ModelState.AddModelError(nameof(m.ImagePath), "Select an approved local image."); if (await db.Categories.AnyAsync(x => x.Slug == m.Slug && x.Id != m.Id)) ModelState.AddModelError(nameof(m.Slug), "This slug is already in use."); if (!ModelState.IsValid) { Populate(); return View("Edit", m); } Category x; if (editing) { x = await db.Categories.FindAsync(m.Id) ?? throw new InvalidOperationException(); } else { x = new Category { Name = m.Name, Slug = m.Slug }; db.Add(x); } x.Name = m.Name; x.Slug = m.Slug; x.Description = m.Description.Trim(); x.ImagePath = m.ImagePath; x.DisplayOrder = m.DisplayOrder; x.IsActive = m.IsActive; await db.SaveChangesAsync(); TempData["Notice"] = $"Category {(editing ? "updated" : "created")}."; return RedirectToAction(nameof(Index)); }
    private void Populate() => ViewBag.Images = Images;
}
