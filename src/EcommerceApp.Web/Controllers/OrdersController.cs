using System.Security.Claims;
using EcommerceApp.Web.Data;
using EcommerceApp.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Controllers;

[Authorize]
public sealed class OrdersController(ApplicationDbContext db) : Controller
{
    [HttpGet("/orders")]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var orders = await db.Orders.AsNoTracking().Where(x => x.CustomerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new OrderListItemModel(x.OrderNumber, x.CreatedAt, x.Status, x.Total, x.Items.Sum(i => i.Quantity)))
            .ToListAsync();
        return View(orders);
    }

    [HttpGet("/orders/{orderNumber}")]
    public async Task<IActionResult> Detail(string orderNumber)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var order = await db.Orders.AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.OrderNumber == orderNumber && x.CustomerId == userId);
        if (order is null) return NotFound();
        return View(new OrderDetailModel(order.OrderNumber, order.CreatedAt, order.Status, order.PaymentStatus,
            order.ContactEmail, order.RecipientName, order.ShippingAddress, order.Subtotal, order.DiscountTotal,
            order.ShippingTotal, order.Total, order.Items.Select(x => new OrderItemModel(x.Sku, x.ProductName, x.UnitPrice, x.Quantity)).ToList()));
    }
}
