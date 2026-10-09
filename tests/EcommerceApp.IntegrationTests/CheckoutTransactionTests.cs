using EcommerceApp.Web.Data;
using EcommerceApp.Web.Domain.Entities;
using EcommerceApp.Web.Services.Checkout;
using EcommerceApp.Web.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class CheckoutTransactionTests(IntegrationTestFactory factory)
{
    [Fact]
    public async Task Duplicate_checkout_token_creates_one_order_and_returns_same_confirmation()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await AddProduct(db, suffix, 3);
        var cart = new Cart { SessionKey = "duplicate-" + suffix, Items = [new CartItem { ProductId = product.Id, Quantity = 1 }] };
        db.Carts.Add(cart); await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
        var input = Input("checkout-" + suffix);
        var first = await service.PlaceAsync(cart.Id, null, input);
        var second = await service.PlaceAsync(cart.Id, null, input);
        Assert.True(first.Succeeded); Assert.True(second.Succeeded);
        Assert.Equal(first.ConfirmationToken, second.ConfirmationToken);
        Assert.Equal(1, await db.Orders.CountAsync(x => x.ConfirmationToken == first.ConfirmationToken));
        var order = await db.Orders.AsNoTracking().Include(x => x.StatusHistory).Include(x => x.PaymentAttempts).SingleAsync(x => x.ConfirmationToken == first.ConfirmationToken);
        Assert.Equal([OrderStatus.Pending, OrderStatus.Processing], order.StatusHistory.OrderBy(x => x.ChangedAt).Select(x => x.ToStatus));
        var attempt = Assert.Single(order.PaymentAttempts);
        Assert.Equal(PaymentAttemptStatus.Captured, attempt.Status);
        Assert.Equal(order.Total, attempt.Amount);
    }

    [Fact]
    public async Task Unsupported_shipping_country_is_rejected_before_any_stock_is_taken()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await AddProduct(db, "country-" + suffix, 2);
        var cart = new Cart { SessionKey = "country-" + suffix, Items = [new CartItem { ProductId = product.Id, Quantity = 1 }] };
        db.Carts.Add(cart); await db.SaveChangesAsync();
        var input = Input("country-" + suffix); input.CountryCode = "ZZ";
        var result = await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceAsync(cart.Id, null, input);
        Assert.False(result.Succeeded);
        Assert.Contains("ZZ", result.Message);
        Assert.Equal(2, await db.Products.AsNoTracking().Where(x => x.Id == product.Id).Select(x => x.StockQuantity).SingleAsync());
    }

    [Fact]
    public async Task Checkout_token_owned_by_another_cart_never_discloses_confirmation_token()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var collision = string.Concat("foreign-", suffix);
        db.Orders.Add(new Order
        {
            OrderNumber = "NST-FOREIGN-" + suffix,
            CheckoutToken = collision,
            ConfirmationToken = string.Concat("secret-", suffix),
            ContactEmail = "foreign-" + suffix + "@example.local",
            RecipientName = "Maya",
            ShipLine1 = "Private address",
            ShipCity = "Austin",
            ShipRegion = "TX",
            ShipPostalCode = "78701",
            ShipCountryCode = "US",
            Subtotal = 25,
            Total = 25,
            Status = OrderStatus.Processing,
            PaymentStatus = PaymentStatus.SimulatedPaid,
            PaymentAttempts = [new() { AttemptNumber = 1, Amount = 25, Status = PaymentAttemptStatus.Captured, GatewayReference = "sim_foreign_" + suffix }]
        });
        var product = await AddProduct(db, "foreign-" + suffix, 2);
        var cart = new Cart { SessionKey = "attacker-" + suffix, Items = [new CartItem { ProductId = product.Id, Quantity = 1 }] };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();

        var result = await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceAsync(cart.Id, null, Input(collision));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ConfirmationToken);
        Assert.NotEqual(string.Concat("secret-", suffix), result.ConfirmationToken);
        Assert.Equal(1, await db.Orders.CountAsync(x => x.CheckoutToken == collision));
    }

    [Fact]
    public async Task Competing_checkouts_for_last_unit_allow_exactly_one_order()
    {
        var suffix = Guid.NewGuid().ToString("N"); int productId; Guid firstCart; Guid secondCart;
        await using (var setup = factory.Services.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await AddProduct(db, suffix, 1); productId = product.Id;
            var a = new Cart { SessionKey = "race-a-" + suffix, Items = [new CartItem { ProductId = productId, Quantity = 1 }] };
            var b = new Cart { SessionKey = "race-b-" + suffix, Items = [new CartItem { ProductId = productId, Quantity = 1 }] };
            db.AddRange(a, b); await db.SaveChangesAsync(); firstCart = a.Id; secondCart = b.Id;
        }
        async Task<CheckoutResult> Place(Guid cartId, string token)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceAsync(cartId, null, Input(token));
        }
        var results = await Task.WhenAll(Place(firstCart, "race-one-" + suffix), Place(secondCart, "race-two-" + suffix));
        Assert.Single(results, x => x.Succeeded);
        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await verifyDb.Products.Where(x => x.Id == productId).Select(x => x.StockQuantity).SingleAsync());
        Assert.Equal(1, await verifyDb.OrderItems.CountAsync(x => x.ProductId == productId));
    }

    private static CheckoutModel Input(string token) => new() { CheckoutToken = token, ContactEmail = "guest@example.local", RecipientName = "Guest Buyer", Line1 = "10 Test Street", City = "Austin", Region = "TX", PostalCode = "78701", CountryCode = "US", PaymentMethod = "simulated" };
    private static async Task<Product> AddProduct(ApplicationDbContext db, string suffix, int stock)
    {
        var category = new Category { Name = "Checkout " + suffix, Slug = "checkout-" + suffix };
        var product = new Product { Category = category, Name = "Final unit", Slug = "final-" + suffix, Sku = $"TEST-{Guid.NewGuid():N}", Price = 25, StockQuantity = stock };
        db.Add(product); await db.SaveChangesAsync(); return product;
    }
}
