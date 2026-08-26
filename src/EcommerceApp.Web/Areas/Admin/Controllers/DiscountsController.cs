using EcommerceApp.Web.Areas.Admin.ViewModels;
using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Areas.Admin.Controllers;

public sealed class DiscountsController(ApplicationDbContext db) : AdminControllerBase
{
    [HttpGet] public async Task<IActionResult> Index() => View(await db.DiscountCodes.AsNoTracking().Include(x => x.Redemptions).OrderBy(x => x.Code).ToListAsync());
    [HttpGet] public IActionResult Create() => View("Edit", new DiscountEditModel());
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Create(DiscountEditModel model) => Save(model, false);
    [HttpGet] public async Task<IActionResult> Edit(int id) { var x = await db.DiscountCodes.FindAsync(id); return x is null ? NotFound() : View(new DiscountEditModel { Id = x.Id, Code = x.Code, Kind = x.Kind, Value = x.Value, MinimumSubtotal = x.MinimumSubtotal, MaximumDiscount = x.MaximumDiscount, StartsAt = x.StartsAt, EndsAt = x.EndsAt, IsActive = x.IsActive, UsageLimit = x.UsageLimit }); }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Edit(int id, DiscountEditModel model) { if (id != model.Id) return Task.FromResult<IActionResult>(NotFound()); return Save(model, true); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Delete(int id) { var x = await db.DiscountCodes.Include(d => d.Redemptions).SingleOrDefaultAsync(d => d.Id == id); if (x is null) return NotFound(); if (x.Redemptions.Count > 0) { x.IsActive = false; TempData["Error"] = "A redeemed discount cannot be deleted and was deactivated instead."; } else { db.Remove(x); TempData["Notice"] = "Discount deleted."; } await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    private async Task<IActionResult> Save(DiscountEditModel m, bool editing) { m.Code = m.Code.Trim().ToUpperInvariant(); if (await db.DiscountCodes.AnyAsync(x => x.NormalizedCode == m.Code && x.Id != m.Id)) ModelState.AddModelError(nameof(m.Code), "This code is already in use."); if (!ModelState.IsValid) return View("Edit", m); DiscountCode x; if (editing) x = await db.DiscountCodes.FindAsync(m.Id) ?? throw new InvalidOperationException(); else { x = new DiscountCode { Code = m.Code, StartsAt = m.StartsAt, EndsAt = m.EndsAt }; db.Add(x); } x.Code = m.Code; x.NormalizedCode = m.Code; x.Kind = m.Kind; x.Value = m.Value; x.MinimumSubtotal = m.MinimumSubtotal; x.MaximumDiscount = m.MaximumDiscount; x.StartsAt = m.StartsAt; x.EndsAt = m.EndsAt; x.IsActive = m.IsActive; x.UsageLimit = m.UsageLimit; await db.SaveChangesAsync(); TempData["Notice"] = $"Discount {(editing ? "updated" : "created")}."; return RedirectToAction(nameof(Index)); }
}
