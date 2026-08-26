using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.UnitTests;

public sealed class ModelConfigurationTests
{
    private static readonly ApplicationDbContext Db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase("metadata").Options);

    [Fact]
    public void Product_has_unique_slug_sku_precision_and_stock_concurrency()
    {
        var entity = Db.Model.FindEntityType(typeof(Product))!;
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(Product.Slug));
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(Product.Sku));
        Assert.Equal(18, entity.FindProperty(nameof(Product.Price))!.GetPrecision());
        Assert.Equal(2, entity.FindProperty(nameof(Product.Price))!.GetScale());
        Assert.True(entity.FindProperty(nameof(Product.StockVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void Commerce_entities_and_relationships_are_present()
    {
        Assert.NotNull(Db.Model.FindEntityType(typeof(Cart)));
        Assert.NotNull(Db.Model.FindEntityType(typeof(CartItem)));
        Assert.NotNull(Db.Model.FindEntityType(typeof(DiscountCode)));
        Assert.NotNull(Db.Model.FindEntityType(typeof(Order)));
        Assert.NotNull(Db.Model.FindEntityType(typeof(OrderItem)));
        Assert.NotNull(Db.Model.FindEntityType(typeof(Address)));
    }

    [Fact]
    public void Milestone_two_tokens_and_normalized_discount_are_unique()
    {
        var discount = Db.Model.FindEntityType(typeof(DiscountCode))!;
        Assert.Contains(discount.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(DiscountCode.NormalizedCode));
        var order = Db.Model.FindEntityType(typeof(Order))!;
        Assert.Contains(order.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(Order.CheckoutToken));
        Assert.Contains(order.GetIndexes(), x => x.IsUnique && x.Properties.Single().Name == nameof(Order.ConfirmationToken));
    }
}
