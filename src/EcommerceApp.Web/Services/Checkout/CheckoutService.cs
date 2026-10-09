using System.Data;
using System.Security.Cryptography;
using System.Text;
using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using EcommerceApp.Web.Services.Discounts;
using EcommerceApp.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Services.Checkout;

public sealed record CheckoutResult(bool Succeeded, string? ConfirmationToken, string Message);
public interface ICheckoutService { Task<CheckoutResult> PlaceAsync(Guid cartId, string? userId, CheckoutModel input, CancellationToken ct = default); }

public sealed class CheckoutService(ApplicationDbContext db, IDiscountService discounts) : ICheckoutService
{
    public async Task<CheckoutResult> PlaceAsync(Guid cartId, string? userId, CheckoutModel input, CancellationToken ct = default)
    {
        if (input.PaymentMethod != "simulated") return new(false, null, "Only simulated payment is available.");
        var country = input.CountryCode.Trim().ToUpperInvariant();
        if (await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM ref.Countries WHERE CountryCode = {country}").SingleAsync(ct) == 0) return new(false, null, $"We don't ship to country code {country} yet.");
        var ownedCheckoutToken = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{cartId:N}:{input.CheckoutToken}"))).ToLowerInvariant();
        var duplicate = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.CheckoutToken == ownedCheckoutToken, ct);
        if (duplicate is not null && duplicate.CustomerId == userId) return new(true, duplicate.ConfirmationToken, "Order already placed.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var cart = await db.Carts.Include(x => x.Items).ThenInclude(x => x.Product).SingleOrDefaultAsync(x => x.Id == cartId && (userId != null ? x.UserId == userId : x.UserId == null), ct);
            if (cart is null || cart.Items.Count == 0) return new(false, null, "Your bag is empty.");
            if (cart.Items.Any(x => !x.Product.IsActive)) return new(false, null, "An item is no longer available.");
            var subtotal = cart.Items.Sum(x => x.Product.Price * x.Quantity);
            DiscountCode? discountCode = null; var discountAmount = 0m;
            if (cart.DiscountCode is not null)
            {
                var evaluated = await discounts.EvaluateAsync(cart.DiscountCode, subtotal, ct);
                if (!evaluated.Result.IsValid) return new(false, null, evaluated.Result.Message);
                discountCode = evaluated.Code; discountAmount = evaluated.Result.Amount;
            }
            foreach (var item in cart.Items)
            {
                var changed = await db.Products.Where(x => x.Id == item.ProductId && x.IsActive && x.StockQuantity >= item.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.StockQuantity, x => x.StockQuantity - item.Quantity).SetProperty(x => x.StockVersion, x => x.StockVersion + 1), ct);
                if (changed != 1) return new(false, null, $"There is not enough stock for {item.Product.Name}.");
            }
            var shipping = subtotal - discountAmount >= 100 ? 0 : 8m; var now = DateTimeOffset.UtcNow; var total = subtotal - discountAmount + shipping;
            var order = new Order
            {
                OrderNumber = $"NST-{DateTime.UtcNow:yyMMdd}-{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4))}",
                CheckoutToken = ownedCheckoutToken,
                ConfirmationToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
                CustomerId = userId,
                ContactEmail = input.ContactEmail.Trim(),
                RecipientName = input.RecipientName.Trim(),
                ShipLine1 = input.Line1.Trim(),
                ShipLine2 = string.IsNullOrWhiteSpace(input.Line2) ? null : input.Line2.Trim(),
                ShipCity = input.City.Trim(),
                ShipRegion = input.Region.Trim(),
                ShipPostalCode = input.PostalCode.Trim(),
                ShipCountryCode = country,
                Subtotal = subtotal,
                DiscountTotal = discountAmount,
                ShippingTotal = shipping,
                Total = total,
                DiscountCodeSnapshot = discountCode?.Code,
                Status = OrderStatus.Processing,
                PaymentStatus = PaymentStatus.SimulatedPaid,
                CreatedAt = now,
                Items = cart.Items.Select(x => new OrderItem { ProductId = x.ProductId, Sku = x.Product.Sku, ProductName = x.Product.Name, UnitPrice = x.Product.Price, Quantity = x.Quantity }).ToList(),
                // Audit trail and payment record, written in the same transaction as the order.
                StatusHistory = [new() { ToStatus = OrderStatus.Pending, ReasonId = StatusChangeReasons.OrderPlaced, ChangedByUserId = userId, ChangedAt = now }, new() { FromStatus = OrderStatus.Pending, ToStatus = OrderStatus.Processing, ReasonId = StatusChangeReasons.PaymentCaptured, ChangedAt = now.AddTicks(1) }],
                PaymentAttempts = [new() { AttemptNumber = 1, Amount = total, Status = PaymentAttemptStatus.Captured, GatewayReference = "sim_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant(), AttemptedAt = now }]
            };
            db.Orders.Add(order);
            if (discountCode is not null) db.DiscountRedemptions.Add(new DiscountRedemption { DiscountCode = discountCode, Order = order, RedeemedAt = now });
            db.Carts.Remove(cart); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new(true, order.ConfirmationToken, "Order placed.");
        }
        catch (Exception ex) when (IsDatabaseFailure(ex)) { await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); var existing = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.CheckoutToken == ownedCheckoutToken && x.CustomerId == userId, ct); return existing is null ? new(false, null, "Checkout could not be completed. Please try again.") : new(true, existing.ConfirmationToken, "Order already placed."); }
    }
    private static bool IsDatabaseFailure(Exception ex) => ex is DbUpdateException or System.Data.Common.DbException || ex.InnerException is not null && IsDatabaseFailure(ex.InnerException);
}
