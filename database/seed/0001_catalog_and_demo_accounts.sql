/*
    0001_catalog_and_demo_accounts.sql   (development seed, runs once)
    The storefront catalog (8 categories, 50 products), discount codes, roles, and the four
    documented demo accounts with their example orders. Values match the original
    application seeder, so existing tests and docs keep working.

    Demo password for every seeded account: LocalDemo!2026 (development only).
    The hash below is an ASP.NET Core Identity V3 hash (PBKDF2-HMAC-SHA512, 100,000
    iterations) of that password with a fixed salt, so the seed is reproducible.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

DECLARE @PasswordHash nvarchar(max) = N'AQAAAAIAAYagAAAAEBwrg/3HgJAjpwznI/fl4t7VCUyxigCRtCoAgigorGIDFKV+0GAALqnGpw2DFxbCSg==';

/* ------------------------------------------------------------ catalog */
SET IDENTITY_INSERT catalog.Categories ON;
INSERT catalog.Categories (Id, Name, Slug, Description, ImagePath, DisplayOrder, IsActive) VALUES
    (1, N'Audio',       'audio',       N'Rich sound for work and weekends.',      N'/images/categories/audio.svg',       1, 1),
    (2, N'Home',        'home',        N'Quiet upgrades for considered spaces.',  N'/images/categories/home.svg',        2, 1),
    (3, N'Kitchen',     'kitchen',     N'Tools that earn their counter space.',   N'/images/categories/kitchen.svg',     3, 1),
    (4, N'Workspace',   'workspace',   N'Better objects for focused days.',       N'/images/categories/workspace.svg',   4, 1),
    (5, N'Outdoors',    'outdoors',    N'Reliable essentials beyond the door.',   N'/images/categories/outdoors.svg',    5, 1),
    (6, N'Wellness',    'wellness',    N'Small rituals with lasting value.',      N'/images/categories/wellness.svg',    6, 1),
    (7, N'Travel',      'travel',      N'Organized, durable companions.',         N'/images/categories/travel.svg',      7, 1),
    (8, N'Accessories', 'accessories', N'Useful details, refined.',               N'/images/categories/accessories.svg', 8, 1);
SET IDENTITY_INSERT catalog.Categories OFF;

DECLARE @Products TABLE (n int PRIMARY KEY, CategoryId int NOT NULL, Name nvarchar(120) NOT NULL);
INSERT @Products (n, CategoryId, Name) VALUES
    (1, 1, N'Studio Headphones'), (2, 1, N'Pocket Speaker'), (3, 1, N'Turntable One'), (4, 1, N'Wireless Earbuds'), (5, 1, N'Desk Microphone'), (6, 1, N'Radio Mini'), (7, 1, N'Travel Headphones'),
    (8, 2, N'Linen Throw'), (9, 2, N'Stoneware Vase'), (10, 2, N'Oak Tray'), (11, 2, N'Wool Cushion'), (12, 2, N'Arc Table Lamp'), (13, 2, N'Glass Carafe'), (14, 2, N'Scented Candle'),
    (15, 3, N'Chef Knife'), (16, 3, N'Pour Over Set'), (17, 3, N'Enamel Kettle'), (18, 3, N'Pepper Mill'), (19, 3, N'Prep Board'), (20, 3, N'Tea Infuser'),
    (21, 4, N'Task Lamp'), (22, 4, N'Desk Organizer'), (23, 4, N'Mechanical Keyboard'), (24, 4, N'Felt Desk Mat'), (25, 4, N'Aluminum Stand'), (26, 4, N'Weekly Planner'),
    (27, 5, N'Daypack 18L'), (28, 5, N'Camp Lantern'), (29, 5, N'Trail Bottle'), (30, 5, N'Picnic Blanket'), (31, 5, N'Utility Knife'), (32, 5, N'Travel Hammock'),
    (33, 6, N'Yoga Mat'), (34, 6, N'Recovery Roller'), (35, 6, N'Sleep Mask'), (36, 6, N'Bath Towel Set'), (37, 6, N'Essential Oil Diffuser'), (38, 6, N'Meditation Cushion'),
    (39, 7, N'Weekender Bag'), (40, 7, N'Packing Cubes'), (41, 7, N'Passport Wallet'), (42, 7, N'Cable Pouch'), (43, 7, N'Luggage Tag'), (44, 7, N'Travel Pillow'),
    (45, 8, N'Canvas Tote'), (46, 8, N'Leather Card Case'), (47, 8, N'Classic Cap'), (48, 8, N'Merino Scarf'), (49, 8, N'Everyday Watch'), (50, 8, N'Key Organizer');

SET IDENTITY_INSERT catalog.Products ON;
INSERT catalog.Products (Id, CategoryId, Name, Slug, Sku, ShortDescription, Description, Price, StockQuantity, ImagePath, SecondaryImagePath, IsFeatured, IsActive, CreatedAt, UpdatedAt, StockVersion)
SELECT
    p.n, p.CategoryId, p.Name,
    LOWER(REPLACE(p.Name, N' ', N'-')),
    CONCAT('NST-', RIGHT(CONCAT('00', p.n), 3)),
    CONCAT(N'A thoughtfully made ', LOWER(p.Name), N' designed for everyday use.'),
    CONCAT(N'The ', p.Name, N' balances honest materials, purposeful details, and lasting construction. Designed to feel at home in your daily routine and made to be used often.'),
    18 + ((p.n * 17) % 165),
    CASE WHEN p.n % 9 = 0 THEN 0 ELSE 3 + (p.n * 7 % 35) END,
    CONCAT(N'/images/products/', c.Slug, N'.svg'),
    CONCAT(N'/images/categories/', c.Slug, N'.svg'),
    CASE WHEN p.n % 7 = 1 THEN 1 ELSE 0 END,
    1,
    DATEADD(minute, p.n, CAST('2025-01-06T09:00:00+00:00' AS datetimeoffset(7))),
    DATEADD(minute, p.n, CAST('2025-01-06T09:00:00+00:00' AS datetimeoffset(7))),
    0
FROM @Products AS p
JOIN catalog.Categories AS c ON c.Id = p.CategoryId;
SET IDENTITY_INSERT catalog.Products OFF;

/* ------------------------------------------------------------ discount codes */
SET IDENTITY_INSERT sales.DiscountCodes ON;
INSERT sales.DiscountCodes (Id, Code, NormalizedCode, DiscountKindId, Value, MinimumSubtotal, MaximumDiscount, StartsAt, EndsAt, IsActive, UsageLimit) VALUES
    (1, N'WELCOME10', N'WELCOME10', 0, 10, 50,  40,   '2026-01-01T00:00:00+00:00', '2030-01-01T00:00:00+00:00', 1, NULL),
    (2, N'TAKE20',    N'TAKE20',    1, 20, 150, NULL, '2026-01-01T00:00:00+00:00', '2030-01-01T00:00:00+00:00', 1, NULL),
    (3, N'SPRING15',  N'SPRING15',  0, 15, 40,  30,   '2025-03-01T00:00:00+00:00', '2025-06-01T00:00:00+00:00', 0, NULL),
    (4, N'HOLIDAY25', N'HOLIDAY25', 1, 25, 120, NULL, '2025-11-20T00:00:00+00:00', '2026-01-05T00:00:00+00:00', 0, 300),
    (5, N'VIP30',     N'VIP30',     0, 30, 200, 60,   '2026-01-01T00:00:00+00:00', '2027-01-01T00:00:00+00:00', 1, 50);
SET IDENTITY_INSERT sales.DiscountCodes OFF;

/* ------------------------------------------------------------ roles and demo accounts */
INSERT auth.Roles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES
    (N'6f1e2d3c-0001-4a5b-9c8d-7e6f5a4b3c2d', N'Administrator', N'ADMINISTRATOR', N'2f0c9a1e-7a3b-4c55-9d10-1b2c3d4e5f60'),
    (N'6f1e2d3c-0002-4a5b-9c8d-7e6f5a4b3c2d', N'Customer',      N'CUSTOMER',      N'7b8c9d0e-1f2a-4b3c-8d4e-5f6a7b8c9d0e');

DECLARE @Demo TABLE (Id nvarchar(64), Email nvarchar(256), DisplayName nvarchar(100), RoleId nvarchar(64), Phone nvarchar(32), CreatedAt datetimeoffset(7));
INSERT @Demo VALUES
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000001', N'admin@northstar.local', N'Avery Admin', N'6f1e2d3c-0001-4a5b-9c8d-7e6f5a4b3c2d', NULL,              '2025-01-02T09:00:00+00:00'),
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000002', N'leo@example.local',     N'Leo Martins', N'6f1e2d3c-0002-4a5b-9c8d-7e6f5a4b3c2d', N'+1-512-555-0142', '2025-02-10T15:30:00+00:00'),
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000003', N'maya@example.local',    N'Maya Chen',   N'6f1e2d3c-0002-4a5b-9c8d-7e6f5a4b3c2d', N'+1-503-555-0177', '2025-03-04T11:05:00+00:00'),
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000004', N'sam@example.local',     N'Sam Rivera',  N'6f1e2d3c-0002-4a5b-9c8d-7e6f5a4b3c2d', N'+1-718-555-0119', '2025-04-21T18:45:00+00:00');

INSERT auth.Users (Id, DisplayName, CreatedAt, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
SELECT Id, DisplayName, CreatedAt, Email, UPPER(Email), Email, UPPER(Email), 1, @PasswordHash,
       CONVERT(nvarchar(64), HASHBYTES('SHA2_256', CONCAT('stamp:', Id)), 2),
       LOWER(CONVERT(nvarchar(36), CAST(HASHBYTES('MD5', CONCAT('concurrency:', Id)) AS uniqueidentifier))),
       Phone, 0, 0, 1, 0
FROM @Demo;

INSERT auth.UserRoles (UserId, RoleId) SELECT Id, RoleId FROM @Demo;

INSERT customer.Addresses (UserId, Label, RecipientName, Line1, City, Region, PostalCode, CountryCode, CreatedAt) VALUES
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000002', N'Home', N'Leo Martins', N'120 Market Street', N'Austin',   N'TX', N'78701', 'US', '2025-02-10T15:35:00+00:00'),
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000003', N'Home', N'Maya Chen',   N'137 Market Street', N'Portland', N'OR', N'97205', 'US', '2025-03-04T11:10:00+00:00'),
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000004', N'Home', N'Sam Rivera',  N'154 Market Street', N'Brooklyn', N'NY', N'11201', 'US', '2025-04-21T18:50:00+00:00');

/* Stored payment methods for the demo customers (tokenised; no card numbers). */
DECLARE @DemoMethods TABLE (UserId nvarchar(64), TypeId tinyint, Token varchar(64), IsDefault bit, BrandId tinyint NULL, Last4 char(4) NULL, ExpMonth tinyint NULL, ExpYear smallint NULL, Holder nvarchar(100) NULL, MaskedEmail nvarchar(256) NULL);
INSERT @DemoMethods VALUES
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000002', 1, 'pm_demo_leo_visa',     1, 1, '4242', 8, 2028, N'Leo Martins', NULL),
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000003', 1, 'pm_demo_maya_mc',      1, 2, '4444', 3, 2029, N'Maya Chen',   NULL),
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000003', 2, 'pm_demo_maya_paypal',  0, NULL, NULL, NULL, NULL, NULL, N'm***@example.local'),
    (N'a3d5e7f9-0000-4c1d-8e2f-000000000004', 1, 'pm_demo_sam_amex',     1, 3, '0005', 11, 2027, N'Sam Rivera', NULL);

INSERT payment.PaymentMethods (UserId, PaymentMethodTypeId, BillingAddressId, GatewayToken, IsDefault, CreatedAt)
SELECT m.UserId, m.TypeId, a.Id, m.Token, m.IsDefault, DATEADD(minute, 10, a.CreatedAt)
FROM @DemoMethods AS m
JOIN customer.Addresses AS a ON a.UserId = m.UserId;

INSERT payment.CardDetails (PaymentMethodId, PaymentMethodTypeId, CardBrandId, Last4, ExpMonth, ExpYear, CardholderName)
SELECT pm.Id, m.TypeId, m.BrandId, m.Last4, m.ExpMonth, m.ExpYear, m.Holder
FROM @DemoMethods AS m JOIN payment.PaymentMethods AS pm ON pm.GatewayToken = m.Token
WHERE m.BrandId IS NOT NULL;

INSERT payment.WalletDetails (PaymentMethodId, PaymentMethodTypeId, AccountEmailMasked)
SELECT pm.Id, m.TypeId, m.MaskedEmail
FROM @DemoMethods AS m JOIN payment.PaymentMethods AS pm ON pm.GatewayToken = m.Token
WHERE m.MaskedEmail IS NOT NULL;

/* ------------------------------------------------------------ demo orders
   NST-2026-0001 Leo  - shipped
   NST-2026-0002 Maya - delivered
   NST-2026-0003 Sam  - cancelled at the customer's request and refunded */
DECLARE @DemoOrders TABLE (OrderNumber varchar(64), Seq int, UserId nvarchar(64), ProductId int, OrderStatusId tinyint, PaymentStatusId tinyint, CreatedAt datetimeoffset(7));
INSERT @DemoOrders VALUES
    ('NST-2026-0001', 0, N'a3d5e7f9-0000-4c1d-8e2f-000000000002', 1, 2, 1, '2026-04-15T14:00:00+00:00'),
    ('NST-2026-0002', 1, N'a3d5e7f9-0000-4c1d-8e2f-000000000003', 2, 3, 1, '2026-05-15T14:00:00+00:00'),
    ('NST-2026-0003', 2, N'a3d5e7f9-0000-4c1d-8e2f-000000000004', 3, 4, 3, '2026-06-15T14:00:00+00:00');

INSERT sales.Orders (OrderNumber, CheckoutToken, ConfirmationToken, CustomerId, ContactEmail, RecipientName, ShipLine1, ShipCity, ShipRegion, ShipPostalCode, ShipCountryCode,
                     Subtotal, DiscountTotal, ShippingTotal, Total, OrderStatusId, PaymentStatusId, CreatedAt)
SELECT d.OrderNumber, CONCAT('seed-checkout-', d.Seq), CONCAT('seed-confirmation-', d.Seq), d.UserId, u.Email, u.DisplayName,
       a.Line1, a.City, a.Region, a.PostalCode, a.CountryCode,
       p.Price, 0, s.Shipping, p.Price + s.Shipping, d.OrderStatusId, d.PaymentStatusId, d.CreatedAt
FROM @DemoOrders AS d
JOIN auth.Users AS u ON u.Id = d.UserId
JOIN customer.Addresses AS a ON a.UserId = d.UserId
JOIN catalog.Products AS p ON p.Id = d.ProductId
CROSS APPLY (SELECT CAST(CASE WHEN p.Price >= 100 THEN 0 ELSE 8 END AS decimal(18, 2)) AS Shipping) AS s;

INSERT sales.OrderItems (OrderId, ProductId, Sku, ProductName, UnitPrice, Quantity)
SELECT o.Id, p.Id, p.Sku, p.Name, p.Price, 1
FROM @DemoOrders AS d JOIN sales.Orders AS o ON o.OrderNumber = d.OrderNumber JOIN catalog.Products AS p ON p.Id = d.ProductId;

INSERT payment.PaymentAttempts (OrderId, AttemptNumber, PaymentMethodId, Amount, CurrencyCode, PaymentAttemptStatusId, GatewayReference, AttemptedAt)
SELECT o.Id, 1, pm.Id, o.Total, 'USD', 1, CONCAT('ch_demo_', d.Seq), DATEADD(second, 20, o.CreatedAt)
FROM @DemoOrders AS d JOIN sales.Orders AS o ON o.OrderNumber = d.OrderNumber
JOIN payment.PaymentMethods AS pm ON pm.UserId = d.UserId AND pm.IsDefault = 1;

INSERT sales.OrderStatusHistory (OrderId, FromStatusId, ToStatusId, StatusChangeReasonId, ChangedByUserId, ChangedAt, Note)
SELECT o.Id, h.FromStatusId, h.ToStatusId, h.ReasonId, CASE WHEN h.ReasonId = 4 THEN d.UserId END, DATEADD(hour, h.HoursAfter, o.CreatedAt), h.Note
FROM @DemoOrders AS d
JOIN sales.Orders AS o ON o.OrderNumber = d.OrderNumber
CROSS APPLY (VALUES
    (CAST(NULL AS tinyint), CAST(0 AS tinyint), CAST(1 AS smallint), 0, CAST(NULL AS nvarchar(500))),
    (0, 1, 2, 0, NULL),
    (1, CASE WHEN d.OrderStatusId = 4 THEN 4 ELSE 2 END, CASE WHEN d.OrderStatusId = 4 THEN 4 ELSE 8 END, 20, CASE WHEN d.OrderStatusId = 4 THEN N'Customer cancelled before dispatch.' END),
    (2, 3, 9, 90, NULL)
) AS h (FromStatusId, ToStatusId, ReasonId, HoursAfter, Note)
WHERE h.ToStatusId <> 3 OR d.OrderStatusId = 3;

-- Make the timestamps of history rows created at the same instant strictly ordered.
UPDATE h SET ChangedAt = DATEADD(second, 30, h.ChangedAt)
FROM sales.OrderStatusHistory AS h WHERE h.FromStatusId = 0 AND h.ToStatusId = 1;

INSERT payment.Refunds (PaymentAttemptId, RefundReasonId, RefundStatusId, Amount, GatewayReference, RequestedAt, CompletedAt)
SELECT pa.Id, 1, 2, pa.Amount, 're_demo_2', DATEADD(hour, 20, o.CreatedAt), DATEADD(hour, 44, o.CreatedAt)
FROM sales.Orders AS o JOIN payment.PaymentAttempts AS pa ON pa.OrderId = o.Id AND pa.PaymentAttemptStatusId = 1
WHERE o.OrderNumber = 'NST-2026-0003';

COMMIT TRANSACTION;
