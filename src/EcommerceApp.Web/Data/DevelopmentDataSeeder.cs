using EcommerceApp.Web.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Web.Data;

public static class DevelopmentDataSeeder
{
    public const string AdminEmail = "admin@northstar.local";
    public const string DemoPassword = "LocalDemo!2026";

    private static readonly (string Name, string Slug, string Description, string[] Products)[] Catalog = [
        ("Audio", "audio", "Rich sound for work and weekends.", ["Studio Headphones", "Pocket Speaker", "Turntable One", "Wireless Earbuds", "Desk Microphone", "Radio Mini", "Travel Headphones"]),
        ("Home", "home", "Quiet upgrades for considered spaces.", ["Linen Throw", "Stoneware Vase", "Oak Tray", "Wool Cushion", "Arc Table Lamp", "Glass Carafe", "Scented Candle"]),
        ("Kitchen", "kitchen", "Tools that earn their counter space.", ["Chef Knife", "Pour Over Set", "Enamel Kettle", "Pepper Mill", "Prep Board", "Tea Infuser"]),
        ("Workspace", "workspace", "Better objects for focused days.", ["Task Lamp", "Desk Organizer", "Mechanical Keyboard", "Felt Desk Mat", "Aluminum Stand", "Weekly Planner"]),
        ("Outdoors", "outdoors", "Reliable essentials beyond the door.", ["Daypack 18L", "Camp Lantern", "Trail Bottle", "Picnic Blanket", "Utility Knife", "Travel Hammock"]),
        ("Wellness", "wellness", "Small rituals with lasting value.", ["Yoga Mat", "Recovery Roller", "Sleep Mask", "Bath Towel Set", "Essential Oil Diffuser", "Meditation Cushion"]),
        ("Travel", "travel", "Organized, durable companions.", ["Weekender Bag", "Packing Cubes", "Passport Wallet", "Cable Pouch", "Luggage Tag", "Travel Pillow"]),
        ("Accessories", "accessories", "Useful details, refined.", ["Canvas Tote", "Leather Card Case", "Classic Cap", "Merino Scarf", "Everyday Watch", "Key Organizer"])
    ];

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        if (!await db.Categories.AnyAsync())
        {
            var productNumber = 1;
            foreach (var (name, slug, description, products) in Catalog.Select((x, i) => x))
            {
                var category = new Category { Name = name, Slug = slug, Description = description, ImagePath = $"/images/categories/{slug}.svg", DisplayOrder = Array.FindIndex(Catalog, x => x.Slug == slug) + 1 };
                db.Categories.Add(category);
                foreach (var productName in products)
                {
                    var productSlug = Slug(productName);
                    db.Products.Add(new Product
                    {
                        Category = category,
                        Name = productName,
                        Slug = productSlug,
                        Sku = $"NST-{productNumber:000}",
                        ShortDescription = $"A thoughtfully made {productName.ToLowerInvariant()} designed for everyday use.",
                        Description = $"The {productName} balances honest materials, purposeful details, and lasting construction. Designed to feel at home in your daily routine and made to be used often.",
                        Price = 18m + ((productNumber * 17) % 165),
                        StockQuantity = productNumber % 9 == 0 ? 0 : 3 + (productNumber * 7 % 35),
                        ImagePath = $"/images/products/{slug}.svg",
                        SecondaryImagePath = $"/images/categories/{slug}.svg",
                        IsFeatured = productNumber % 7 == 1
                    });
                    productNumber++;
                }
            }
            db.DiscountCodes.AddRange(
                new DiscountCode { Code = "WELCOME10", NormalizedCode = "WELCOME10", Kind = DiscountKind.Percentage, Value = 10, MinimumSubtotal = 50, MaximumDiscount = 40, StartsAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"), EndsAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z") },
                new DiscountCode { Code = "TAKE20", NormalizedCode = "TAKE20", Kind = DiscountKind.FixedAmount, Value = 20, MinimumSubtotal = 150, StartsAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z"), EndsAt = DateTimeOffset.Parse("2030-01-01T00:00:00Z") });
            await db.SaveChangesAsync();
        }
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Administrator", "Customer" }) if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (email, name, role) in new[] { (AdminEmail, "Avery Admin", "Administrator"), ("maya@example.local", "Maya Chen", "Customer"), ("leo@example.local", "Leo Martins", "Customer"), ("sam@example.local", "Sam Rivera", "Customer") })
        {
            var user = await users.FindByEmailAsync(email);
            if (user is null) { user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = name, LockoutEnabled = true }; var result = await users.CreateAsync(user, DemoPassword); if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description))); }
            else if (!user.LockoutEnabled) { user.LockoutEnabled = true; await users.UpdateAsync(user); }
            if (!await users.IsInRoleAsync(user, role)) await users.AddToRoleAsync(user, role);
        }
        if (!await db.Addresses.AnyAsync())
        {
            var customers = await db.Users.Where(x => x.Email != AdminEmail).OrderBy(x => x.Email).ToListAsync();
            var cities = new[] { ("Austin", "TX", "78701"), ("Portland", "OR", "97205"), ("Brooklyn", "NY", "11201") };
            for (var i = 0; i < customers.Count; i++) db.Addresses.Add(new Address { UserId = customers[i].Id, RecipientName = customers[i].DisplayName, Line1 = $"{120 + i * 17} Market Street", City = cities[i].Item1, Region = cities[i].Item2, PostalCode = cities[i].Item3 });
            await db.SaveChangesAsync();
        }
        if (!await db.Orders.AnyAsync())
        {
            var customers = await db.Users.Where(x => x.Email != AdminEmail).OrderBy(x => x.Email).ToListAsync();
            var products = await db.Products.OrderBy(x => x.Id).Take(3).ToListAsync();
            for (var i = 0; i < customers.Count; i++)
            {
                var product = products[i];
                db.Orders.Add(new Order { OrderNumber = $"NST-2026-{i + 1:0000}", CheckoutToken = $"seed-checkout-{i}", ConfirmationToken = $"seed-confirmation-{i}", CustomerId = customers[i].Id, ContactEmail = customers[i].Email!, RecipientName = customers[i].DisplayName, ShippingAddress = $"{120 + i * 17} Market Street", Subtotal = product.Price, ShippingTotal = product.Price >= 100 ? 0 : 8, Total = product.Price + (product.Price >= 100 ? 0 : 8), Status = (OrderStatus)(i + 2), PaymentStatus = PaymentStatus.SimulatedPaid, CreatedAt = DateTimeOffset.Parse($"2026-0{i + 4}-15T14:00:00Z"), Items = [new OrderItem { ProductId = product.Id, Sku = product.Sku, ProductName = product.Name, UnitPrice = product.Price, Quantity = 1 }] });
            }
            await db.SaveChangesAsync();
        }
    }

    private static string Slug(string value) => value.ToLowerInvariant().Replace(" ", "-");
}
