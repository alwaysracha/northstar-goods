using EcommerceApp.Web.Domain.Entities;

namespace EcommerceApp.Web.Services.Discounts;

public sealed record DiscountResult(bool IsValid, decimal Amount, string Message)
{
    public static DiscountResult Invalid(string message) => new(false, 0, message);
}

public interface IDiscountService
{
    Task<(DiscountCode? Code, DiscountResult Result)> EvaluateAsync(string? enteredCode, decimal subtotal, CancellationToken cancellationToken = default);
}

public sealed class DiscountService(EcommerceApp.Web.Data.ApplicationDbContext db) : IDiscountService
{
    public async Task<(DiscountCode?, DiscountResult)> EvaluateAsync(string? enteredCode, decimal subtotal, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(enteredCode);
        if (normalized.Length == 0) return (null, DiscountResult.Invalid("Enter a discount code."));
        var code = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(
            db.DiscountCodes, x => x.NormalizedCode == normalized, cancellationToken);
        if (code is null) return (null, DiscountResult.Invalid("That discount code is not valid."));
        var uses = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.DiscountRedemptions.Where(x => x.DiscountCodeId == code.Id), cancellationToken);
        return (code, Evaluate(code, enteredCode!, subtotal, DateTimeOffset.UtcNow, uses));
    }

    public static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();

    public static DiscountResult Evaluate(DiscountCode code, string enteredCode, decimal subtotal, DateTimeOffset now, int redemptionCount)
    {
        if (!StringComparer.OrdinalIgnoreCase.Equals(code.Code.Trim(), enteredCode.Trim())) return DiscountResult.Invalid("That discount code is not valid.");
        if (!code.IsActive) return DiscountResult.Invalid("That discount code is not active.");
        if (now < code.StartsAt || now > code.EndsAt) return DiscountResult.Invalid("That discount code is outside its valid date window.");
        if (subtotal < code.MinimumSubtotal) return DiscountResult.Invalid($"A minimum subtotal of {code.MinimumSubtotal:C} is required.");
        if (code.UsageLimit is not null && redemptionCount >= code.UsageLimit) return DiscountResult.Invalid("That discount code has reached its usage limit.");
        var amount = code.Kind == DiscountKind.Percentage ? subtotal * code.Value / 100m : code.Value;
        if (code.MaximumDiscount is not null) amount = Math.Min(amount, code.MaximumDiscount.Value);
        return new(true, decimal.Round(Math.Min(subtotal, amount), 2), "Discount applied.");
    }
}
