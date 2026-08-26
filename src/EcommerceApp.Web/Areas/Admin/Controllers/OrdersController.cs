using EcommerceApp.Web.Areas.Admin.ViewModels;
using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using EcommerceApp.Web.Services.Orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Areas.Admin.Controllers;

public sealed class OrdersController(ApplicationDbContext db) : AdminControllerBase
{
    [HttpGet] public async Task<IActionResult> Index(string? search, OrderStatus? status) { var q = db.Orders.AsNoTracking().Include(x => x.Items).AsQueryable(); if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => x.OrderNumber.Contains(s) || x.ContactEmail.Contains(s)); } if (status.HasValue) q = q.Where(x => x.Status == status); ViewBag.Search = search; ViewBag.Status = status; return View(await q.OrderByDescending(x => x.CreatedAt).ToListAsync()); }
    [HttpGet("/admin/orders/{id:int}")] public async Task<IActionResult> Detail(int id) { var x = await db.Orders.AsNoTracking().Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id); if (x is null) return NotFound(); ViewBag.NextStatuses = OrderStatusPolicy.Next(x.Status); return View(x); }
    [HttpPost("/admin/orders/{id:int}/status"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(int id, OrderStatusEditModel model)
    {
        if (!ModelState.IsValid || !OrderStatusPolicy.CanTransition(model.OriginalStatus, model.Status))
        {
            TempData["Error"] = $"The transition from {model.OriginalStatus} to {model.Status} is not allowed.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        var changed = await db.Orders.Where(x => x.Id == id && x.Status == model.OriginalStatus)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, model.Status));
        if (changed == 0)
        {
            if (!await db.Orders.AnyAsync(x => x.Id == id)) return NotFound();
            TempData["Error"] = "This order changed after you opened it. Review its current status and try again.";
            return Conflict();
        }
        TempData["Notice"] = "Order status updated.";
        return RedirectToAction(nameof(Detail), new { id });
    }
}
