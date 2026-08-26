using EcommerceApp.Web.Areas.Admin.ViewModels;
using EcommerceApp.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Areas.Admin.Controllers;

public sealed class HomeController(ApplicationDbContext db) : AdminControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(new DashboardModel(
        await db.Categories.CountAsync(), await db.Products.CountAsync(), await db.Products.CountAsync(x => x.IsActive),
        await db.Products.CountAsync(x => x.StockQuantity <= 5), await db.Users.CountAsync(), await db.Orders.CountAsync(),
        await db.Orders.Where(x => x.PaymentStatus == Domain.Entities.PaymentStatus.SimulatedPaid).SumAsync(x => (decimal?)x.Total) ?? 0));
}
