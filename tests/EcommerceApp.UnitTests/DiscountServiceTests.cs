using EcommerceApp.Web.Domain.Entities;
using EcommerceApp.Web.Services.Discounts;

namespace EcommerceApp.UnitTests;

public sealed class DiscountServiceTests
{
    [Fact]
    public void Evaluate_matches_case_insensitively_and_caps_percentage()
    {
        var code = new DiscountCode { Code = "SAVE20", Kind = DiscountKind.Percentage, Value = 20, MinimumSubtotal = 50, MaximumDiscount = 15, StartsAt = DateTimeOffset.UtcNow.AddDays(-1), EndsAt = DateTimeOffset.UtcNow.AddDays(1) };
        var result = DiscountService.Evaluate(code, " save20 ", 100, DateTimeOffset.UtcNow, 0);
        Assert.True(result.IsValid);
        Assert.Equal(15m, result.Amount);
    }

    [Theory]
    [InlineData(false, 100, 0, "not active")]
    [InlineData(true, 40, 0, "minimum")]
    [InlineData(true, 100, 2, "usage limit")]
    public void Evaluate_rejects_ineligible_codes(bool active, decimal subtotal, int uses, string message)
    {
        var now = DateTimeOffset.UtcNow;
        var code = new DiscountCode { Code = "TEN", Kind = DiscountKind.FixedAmount, Value = 10, MinimumSubtotal = 50, StartsAt = now.AddDays(-1), EndsAt = now.AddDays(1), IsActive = active, UsageLimit = 2 };
        var result = DiscountService.Evaluate(code, "ten", subtotal, now, uses);
        Assert.False(result.IsValid);
        Assert.Contains(message, result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
