/*
    0002_customers_and_order_history.sql   (development seed, runs once)
    Generates 1,000 customers and ~5,200 orders covering 2024-06 .. 2026-10, deterministically
    (every pseudo-random choice comes from SHA-256 of a fixed key, so every run produces
    identical data).

    Per customer: 1-3 addresses, 1-3 tokenised payment methods (cards, wallets, PayPal, ACH).
    Order outcomes (approximate share):
      78%  paid on the first attempt -> processing / shipped / delivered by age
       6%  first attempt declined, second captured ("recovered")
       5%  paid, then cancelled (customer request, stock, address, fraud review) and refunded
     6.5%  payment failed: 1-3 declined/errored attempts, each with a documented reason
           pending: ~25% of orders from the last 72 hours await bank / 3-D Secure confirmation
    ~9% of delivered orders have a return (full or partial), most refunded.

    All generated customers use the demo password LocalDemo!2026 (development only).
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

DECLARE @CustomerCount int = 1000;
DECLARE @OrderCount    int = 5200;
DECLARE @SeedNow       datetimeoffset(7) = '2026-10-08T12:00:00+00:00'; -- fixed "now" keeps the data reproducible
DECLARE @PasswordHash  nvarchar(max) = N'AQAAAAIAAYagAAAAEBwrg/3HgJAjpwznI/fl4t7VCUyxigCRtCoAgigorGIDFKV+0GAALqnGpw2DFxbCSg==';
DECLARE @CustomerRole  nvarchar(64) = (SELECT Id FROM auth.Roles WHERE NormalizedName = N'CUSTOMER');
DECLARE @AdminId       nvarchar(64) = (SELECT Id FROM auth.Users WHERE NormalizedEmail = N'ADMIN@NORTHSTAR.LOCAL');

/* ------------------------------------------------------------ word lists */
CREATE TABLE #First (i int PRIMARY KEY, Name nvarchar(40) COLLATE DATABASE_DEFAULT NOT NULL);
INSERT #First SELECT ordinal - 1, value FROM STRING_SPLIT(N'Olivia,Liam,Emma,Noah,Ava,Elijah,Sophia,James,Isabella,William,Mia,Benjamin,Charlotte,Lucas,Amelia,Henry,Harper,Theodore,Evelyn,Jack,Abigail,Levi,Emily,Alexander,Ella,Jackson,Elizabeth,Mateo,Camila,Daniel,Luna,Michael,Sofia,Mason,Avery,Sebastian,Mila,Ethan,Aria,Logan,Scarlett,Owen,Penelope,Samuel,Layla,Jacob,Chloe,Asher,Victoria,Aiden,Madison,John,Eleanor,Joseph,Grace,Wyatt,Nora,David,Riley,Leo,Priya,Arjun,Mei,Hiroshi,Fatima,Omar,Ana,Diego,Zoe,Kai', N',', 1);
CREATE TABLE #Last (i int PRIMARY KEY, Name nvarchar(40) COLLATE DATABASE_DEFAULT NOT NULL);
INSERT #Last SELECT ordinal - 1, value FROM STRING_SPLIT(N'Smith,Johnson,Williams,Brown,Jones,Garcia,Miller,Davis,Rodriguez,Martinez,Hernandez,Lopez,Gonzalez,Wilson,Anderson,Thomas,Taylor,Moore,Jackson,Martin,Lee,Perez,Thompson,White,Harris,Sanchez,Clark,Ramirez,Lewis,Robinson,Walker,Young,Allen,King,Wright,Scott,Torres,Nguyen,Hill,Flores,Green,Adams,Nelson,Baker,Hall,Rivera,Campbell,Mitchell,Carter,Roberts,Patel,Shah,Kim,Chen,Singh,Okafor,Larsen,Novak,Rossi,Murphy', N',', 1);
CREATE TABLE #Street (i int PRIMARY KEY, Name nvarchar(60) COLLATE DATABASE_DEFAULT NOT NULL);
INSERT #Street SELECT ordinal - 1, value FROM STRING_SPLIT(N'Maple Avenue,Oak Street,Cedar Lane,Pine Street,Elm Street,Washington Avenue,Lake Drive,Hillcrest Road,Park Avenue,River Road,Sunset Boulevard,Highland Avenue,Willow Way,Spring Street,Chestnut Street,Meadow Lane,Forest Drive,Lincoln Street,Franklin Avenue,Harbor View', N',', 1);
CREATE TABLE #City (i int PRIMARY KEY, City nvarchar(80) COLLATE DATABASE_DEFAULT NOT NULL, Region nvarchar(80) COLLATE DATABASE_DEFAULT NOT NULL, Zip char(5) COLLATE DATABASE_DEFAULT NOT NULL, AreaCode char(3) COLLATE DATABASE_DEFAULT NOT NULL);
INSERT #City VALUES
    (0, N'New York', N'NY', '10001', '212'), (1, N'Los Angeles', N'CA', '90012', '213'), (2, N'Chicago', N'IL', '60601', '312'),
    (3, N'Houston', N'TX', '77002', '713'), (4, N'Phoenix', N'AZ', '85004', '602'), (5, N'Philadelphia', N'PA', '19103', '215'),
    (6, N'San Antonio', N'TX', '78205', '210'), (7, N'San Diego', N'CA', '92101', '619'), (8, N'Dallas', N'TX', '75201', '214'),
    (9, N'Austin', N'TX', '78701', '512'), (10, N'Seattle', N'WA', '98101', '206'), (11, N'Denver', N'CO', '80202', '303'),
    (12, N'Boston', N'MA', '02108', '617'), (13, N'Nashville', N'TN', '37203', '615'), (14, N'Portland', N'OR', '97205', '503'),
    (15, N'Atlanta', N'GA', '30303', '404'), (16, N'Miami', N'FL', '33130', '305'), (17, N'Minneapolis', N'MN', '55401', '612'),
    (18, N'Raleigh', N'NC', '27601', '919'), (19, N'Salt Lake City', N'UT', '84101', '801'), (20, N'Kansas City', N'MO', '64105', '816'),
    (21, N'Columbus', N'OH', '43215', '614'), (22, N'Pittsburgh', N'PA', '15222', '412'), (23, N'Madison', N'WI', '53703', '608'),
    (24, N'Brooklyn', N'NY', '11201', '718'), (25, N'Oakland', N'CA', '94612', '510'), (26, N'Tampa', N'FL', '33602', '813'),
    (27, N'Richmond', N'VA', '23219', '804'), (28, N'Albuquerque', N'NM', '87102', '505'), (29, N'Boise', N'ID', '83702', '208');
CREATE TABLE #Bank (i int PRIMARY KEY, Name nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL);
INSERT #Bank SELECT ordinal - 1, value FROM STRING_SPLIT(N'Chase,Bank of America,Wells Fargo,Citibank,US Bank,PNC Bank,Capital One,Ally Bank', N',', 1);

DECLARE @FirstCount int = (SELECT COUNT(*) FROM #First), @LastCount int = (SELECT COUNT(*) FROM #Last),
        @StreetCount int = (SELECT COUNT(*) FROM #Street), @CityCount int = (SELECT COUNT(*) FROM #City), @BankCount int = (SELECT COUNT(*) FROM #Bank);

/* ------------------------------------------------------------ numbers */
CREATE TABLE #N (n int PRIMARY KEY);
WITH d AS (SELECT x FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) AS v (x))
INSERT #N (n) SELECT a.x * 1000 + b.x * 100 + c.x * 10 + e.x + 1 FROM d AS a CROSS JOIN d AS b CROSS JOIN d AS c CROSS JOIN d AS e;

/* ------------------------------------------------------------ customers */
CREATE TABLE #Customer
(
    n int PRIMARY KEY, Id nvarchar(64) COLLATE DATABASE_DEFAULT NOT NULL, FirstName nvarchar(40) COLLATE DATABASE_DEFAULT NOT NULL, LastName nvarchar(40) COLLATE DATABASE_DEFAULT NOT NULL,
    Email nvarchar(256) COLLATE DATABASE_DEFAULT NOT NULL, Phone nvarchar(32) COLLATE DATABASE_DEFAULT NULL, CreatedAt datetimeoffset(7) NOT NULL, CityIdx int NOT NULL,
    AddressCount int NOT NULL, MethodCount int NOT NULL, r1 int NOT NULL, r2 int NOT NULL, r3 int NOT NULL
);
INSERT #Customer
SELECT
    n.n,
    LOWER(CONVERT(nvarchar(36), CAST(HASHBYTES('MD5', CONCAT('northstar-customer-', n.n)) AS uniqueidentifier))),
    f.Name, l.Name,
    LOWER(CONCAT(f.Name, N'.', l.Name, N'.', RIGHT(CONCAT('000', n.n), 4), N'@example.com')),
    CASE WHEN r.r4 % 10 < 7 THEN CONCAT(N'+1-', c.AreaCode, N'-555-01', RIGHT(CONCAT('0', r.r5 % 100), 2)) END,
    DATEADD(minute, r.r3 % DATEDIFF(minute, '2024-06-01', '2026-06-30'), CAST('2024-06-01T00:00:00+00:00' AS datetimeoffset(7))),
    r.r6 % @CityCount,
    CASE WHEN r.r7 % 10 < 6 THEN 1 WHEN r.r7 % 10 < 9 THEN 2 ELSE 3 END,
    CASE WHEN r.r8 % 10 < 5 THEN 1 WHEN r.r8 % 10 < 8 THEN 2 ELSE 3 END,
    r.r4, r.r5, r.r8
FROM #N AS n
CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT('customer:', n.n)) AS h) AS x
CROSS APPLY (SELECT
    CAST(SUBSTRING(x.h, 1, 4) AS int) & 2147483647 AS r1,  CAST(SUBSTRING(x.h, 5, 4) AS int) & 2147483647 AS r2,
    CAST(SUBSTRING(x.h, 9, 4) AS int) & 2147483647 AS r3,  CAST(SUBSTRING(x.h, 13, 4) AS int) & 2147483647 AS r4,
    CAST(SUBSTRING(x.h, 17, 4) AS int) & 2147483647 AS r5, CAST(SUBSTRING(x.h, 21, 4) AS int) & 2147483647 AS r6,
    CAST(SUBSTRING(x.h, 25, 4) AS int) & 2147483647 AS r7, CAST(SUBSTRING(x.h, 29, 4) AS int) & 2147483647 AS r8) AS r
JOIN #First AS f ON f.i = r.r1 % @FirstCount
JOIN #Last AS l ON l.i = r.r2 % @LastCount
JOIN #City AS c ON c.i = r.r6 % @CityCount
WHERE n.n <= @CustomerCount;

INSERT auth.Users (Id, DisplayName, CreatedAt, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
SELECT Id, CONCAT(FirstName, N' ', LastName), CreatedAt, Email, UPPER(Email), Email, UPPER(Email),
       CASE WHEN r1 % 20 = 0 THEN 0 ELSE 1 END, @PasswordHash,
       CONVERT(nvarchar(64), HASHBYTES('SHA2_256', CONCAT('stamp:', Id)), 2),
       LOWER(CONVERT(nvarchar(36), CAST(HASHBYTES('MD5', CONCAT('concurrency:', Id)) AS uniqueidentifier))),
       Phone, CASE WHEN Phone IS NOT NULL AND r2 % 3 = 0 THEN 1 ELSE 0 END, 0, 1, 0
FROM #Customer;

INSERT auth.UserRoles (UserId, RoleId) SELECT Id, @CustomerRole FROM #Customer;

/* ------------------------------------------------------------ addresses (k = 1 is Home) */
CREATE TABLE #Address (n int NOT NULL, k int NOT NULL, AddressId int NULL, PRIMARY KEY (n, k));
INSERT customer.Addresses (UserId, Label, RecipientName, Line1, Line2, City, Region, PostalCode, CountryCode, CreatedAt)
SELECT c.Id,
       CASE k.n WHEN 1 THEN N'Home' WHEN 2 THEN N'Work' ELSE N'Family' END,
       CONCAT(c.FirstName, N' ', c.LastName),
       CONCAT(100 + r.r1 % 9800, N' ', s.Name),
       CASE WHEN r.r2 % 4 = 0 THEN CONCAT(N'Apt ', 1 + r.r3 % 30, CHAR(65 + r.r3 % 6)) WHEN k.n = 2 THEN CONCAT(N'Suite ', 100 + r.r3 % 900) END,
       ct.City, ct.Region, ct.Zip, 'US',
       DATEADD(day, k.n - 1, c.CreatedAt)
FROM #Customer AS c
JOIN #N AS k ON k.n <= c.AddressCount
CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT('address:', c.n, ':', k.n)) AS h) AS x
CROSS APPLY (SELECT CAST(SUBSTRING(x.h, 1, 4) AS int) & 2147483647 AS r1, CAST(SUBSTRING(x.h, 5, 4) AS int) & 2147483647 AS r2,
                    CAST(SUBSTRING(x.h, 9, 4) AS int) & 2147483647 AS r3) AS r
JOIN #Street AS s ON s.i = r.r1 % @StreetCount
JOIN #City AS ct ON ct.i = CASE WHEN k.n = 1 THEN c.CityIdx ELSE (c.CityIdx + k.n * 7) % @CityCount END;

INSERT #Address (n, k, AddressId)
SELECT c.n, CASE a.Label WHEN N'Home' THEN 1 WHEN N'Work' THEN 2 ELSE 3 END, a.Id
FROM customer.Addresses AS a JOIN #Customer AS c ON c.Id = a.UserId;

/* ------------------------------------------------------------ payment methods (k = 1 is the default; only k = 3 is ever removed) */
CREATE TABLE #Method
(
    n int NOT NULL, k int NOT NULL, TypeId tinyint NOT NULL, DetailKind varchar(10) COLLATE DATABASE_DEFAULT NOT NULL, Token varchar(64) COLLATE DATABASE_DEFAULT NOT NULL,
    IsDefault bit NOT NULL, CreatedAt datetimeoffset(7) NOT NULL, RemovedAt datetimeoffset(7) NULL,
    r1 int NOT NULL, r2 int NOT NULL, r3 int NOT NULL, MethodId int NULL, PRIMARY KEY (n, k)
);
INSERT #Method (n, k, TypeId, DetailKind, Token, IsDefault, CreatedAt, RemovedAt, r1, r2, r3)
SELECT c.n, k.n, t.TypeId, pmt.DetailKind,
       CONCAT('pm_', LOWER(LEFT(CONVERT(varchar(64), x.h, 2), 24))),
       CASE WHEN k.n = 1 THEN 1 ELSE 0 END,
       DATEADD(hour, 1 + (k.n - 1) * 24 * 30, c.CreatedAt),
       CASE WHEN k.n = 3 AND r.r2 % 3 = 0 THEN DATEADD(day, -(r.r3 % 60), @SeedNow) END,
       r.r1, r.r2, r.r3
FROM #Customer AS c
JOIN #N AS k ON k.n <= c.MethodCount
CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT('method:', c.n, ':', k.n)) AS h) AS x
CROSS APPLY (SELECT CAST(SUBSTRING(x.h, 1, 4) AS int) & 2147483647 AS r1, CAST(SUBSTRING(x.h, 5, 4) AS int) & 2147483647 AS r2,
                    CAST(SUBSTRING(x.h, 9, 4) AS int) & 2147483647 AS r3, CAST(SUBSTRING(x.h, 13, 4) AS int) & 2147483647 AS r4) AS r
CROSS APPLY (SELECT CAST(CASE WHEN r.r4 % 100 < 70 THEN 1 WHEN r.r4 % 100 < 80 THEN 2 WHEN r.r4 % 100 < 88 THEN 3 WHEN r.r4 % 100 < 95 THEN 4 ELSE 5 END AS tinyint) AS TypeId) AS t
JOIN ref.PaymentMethodTypes AS pmt ON pmt.PaymentMethodTypeId = t.TypeId;

-- A method can't be removed before it was added (customers created late in the window).
UPDATE #Method SET RemovedAt = NULL WHERE RemovedAt < CreatedAt;

INSERT payment.PaymentMethods (UserId, PaymentMethodTypeId, BillingAddressId, GatewayToken, IsDefault, CreatedAt, RemovedAt)
SELECT c.Id, m.TypeId, a.AddressId, m.Token, m.IsDefault, m.CreatedAt, m.RemovedAt
FROM #Method AS m JOIN #Customer AS c ON c.n = m.n JOIN #Address AS a ON a.n = m.n AND a.k = 1;

UPDATE m SET MethodId = pm.Id FROM #Method AS m JOIN payment.PaymentMethods AS pm ON pm.GatewayToken = m.Token;

INSERT payment.CardDetails (PaymentMethodId, PaymentMethodTypeId, CardBrandId, Last4, ExpMonth, ExpYear, CardholderName)
SELECT m.MethodId, m.TypeId,
       CASE WHEN m.r1 % 100 < 50 THEN 1 WHEN m.r1 % 100 < 80 THEN 2 WHEN m.r1 % 100 < 92 THEN 3 ELSE 4 END,
       RIGHT(CONCAT('000', m.r2 % 10000), 4),
       1 + m.r3 % 12,
       2025 + m.r1 % 7,
       CONCAT(c.FirstName, N' ', c.LastName)
FROM #Method AS m JOIN #Customer AS c ON c.n = m.n
WHERE m.DetailKind = 'CARD';

INSERT payment.WalletDetails (PaymentMethodId, PaymentMethodTypeId, AccountEmailMasked)
SELECT m.MethodId, m.TypeId, CONCAT(LOWER(LEFT(c.FirstName, 1)), N'***@', SUBSTRING(c.Email, CHARINDEX(N'@', c.Email) + 1, 100))
FROM #Method AS m JOIN #Customer AS c ON c.n = m.n
WHERE m.DetailKind = 'WALLET';

INSERT payment.BankAccountDetails (PaymentMethodId, PaymentMethodTypeId, BankName, AccountType, AccountLast4)
SELECT m.MethodId, m.TypeId, b.Name, CASE WHEN m.r2 % 4 = 0 THEN 'SAVINGS' ELSE 'CHECKING' END, RIGHT(CONCAT('000', m.r3 % 10000), 4)
FROM #Method AS m JOIN #Bank AS b ON b.i = m.r1 % @BankCount
WHERE m.DetailKind = 'BANK';

/* ------------------------------------------------------------ orders: header facts
   Outcome: S = paid first time, R = recovered after a decline, C = paid then cancelled,
            F = payment failed, P = payment pending */
CREATE TABLE #Order
(
    n int PRIMARY KEY, CustomerN int NOT NULL, CreatedAt datetimeoffset(7) NOT NULL, Outcome char(1) COLLATE DATABASE_DEFAULT NOT NULL,
    LineCount int NOT NULL, AddressK int NOT NULL, MethodK int NOT NULL,
    r1 int NOT NULL, r2 int NOT NULL, r3 int NOT NULL, r4 int NOT NULL, r5 int NOT NULL, r6 int NOT NULL,
    Subtotal decimal(18, 2) NULL, DiscountCodeId int NULL, DiscountTotal decimal(18, 2) NOT NULL DEFAULT 0,
    ShippingTotal decimal(18, 2) NULL, Total decimal(18, 2) NULL,
    CaptureAt datetimeoffset(7) NULL, ShipAt datetimeoffset(7) NULL, DeliverAt datetimeoffset(7) NULL, CancelAt datetimeoffset(7) NULL,
    OrderStatusId tinyint NULL, PaymentStatusId tinyint NULL, FailedAttempts int NOT NULL DEFAULT 0, OrderId int NULL
);
INSERT #Order (n, CustomerN, CreatedAt, Outcome, LineCount, AddressK, MethodK, r1, r2, r3, r4, r5, r6)
SELECT o.n, c.n, d.CreatedAt, oc.Outcome,
       CASE WHEN r.r5 % 100 < 55 THEN 1 WHEN r.r5 % 100 < 82 THEN 2 WHEN r.r5 % 100 < 95 THEN 3 ELSE 4 END,
       CASE WHEN r.r6 % 10 < 8 OR c.AddressCount = 1 THEN 1 ELSE 2 END,
       CASE WHEN r.r7 % 5 = 0 AND c.MethodCount >= 2 THEN 2 ELSE 1 END,
       r.r1, r.r2, r.r4, r.r5, r.r6, r.r8
FROM #N AS o
CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT('order:', o.n)) AS h) AS x
CROSS APPLY (SELECT
    CAST(SUBSTRING(x.h, 1, 4) AS int) & 2147483647 AS r1,  CAST(SUBSTRING(x.h, 5, 4) AS int) & 2147483647 AS r2,
    CAST(SUBSTRING(x.h, 9, 4) AS int) & 2147483647 AS r3,  CAST(SUBSTRING(x.h, 13, 4) AS int) & 2147483647 AS r4,
    CAST(SUBSTRING(x.h, 17, 4) AS int) & 2147483647 AS r5, CAST(SUBSTRING(x.h, 21, 4) AS int) & 2147483647 AS r6,
    CAST(SUBSTRING(x.h, 25, 4) AS int) & 2147483647 AS r7, CAST(SUBSTRING(x.h, 29, 4) AS int) & 2147483647 AS r8) AS r
JOIN #Customer AS c ON c.n = 1 + r.r1 % @CustomerCount
CROSS APPLY (SELECT DATEADD(minute, r.r3 % (DATEDIFF(minute, c.CreatedAt, @SeedNow) - 90), c.CreatedAt) AS CreatedAt) AS d
-- Only orders from the last 72 hours can still be awaiting payment confirmation.
CROSS APPLY (SELECT CASE WHEN d.CreatedAt >= DATEADD(hour, -72, @SeedNow) AND r.r4 % 1000 < 250 THEN 'P'
                         WHEN r.r4 % 1000 < 65 THEN 'F' WHEN r.r4 % 1000 < 125 THEN 'R'
                         WHEN r.r4 % 1000 < 175 THEN 'C' ELSE 'S' END AS Outcome) AS oc
WHERE o.n <= @OrderCount;

-- An order can only use an address or payment method the customer already had.
UPDATE o SET AddressK = 1 FROM #Order AS o JOIN customer.Addresses AS a ON a.Id = (SELECT AddressId FROM #Address WHERE n = o.CustomerN AND k = 2)
WHERE o.AddressK = 2 AND o.CreatedAt < a.CreatedAt;
UPDATE o SET MethodK = 1 FROM #Order AS o JOIN #Method AS m ON m.n = o.CustomerN AND m.k = 2
WHERE o.MethodK = 2 AND o.CreatedAt < m.CreatedAt;

/* ------------------------------------------------------------ order lines (distinct products per order) */
CREATE TABLE #Line (n int NOT NULL, k int NOT NULL, ProductId int NOT NULL, Quantity int NOT NULL, UnitPrice decimal(18, 2) NOT NULL, Sku varchar(40) COLLATE DATABASE_DEFAULT NOT NULL, ProductName nvarchar(120) COLLATE DATABASE_DEFAULT NOT NULL, PRIMARY KEY (n, k));
INSERT #Line
SELECT o.n, k.n, p.Id,
       CASE WHEN q.r % 100 < 75 THEN 1 WHEN q.r % 100 < 93 THEN 2 ELSE 3 END,
       p.Price, p.Sku, p.Name
FROM #Order AS o
JOIN #N AS k ON k.n <= o.LineCount
CROSS APPLY (SELECT CAST(CAST(HASHBYTES('SHA2_256', CONCAT('line:', o.n, ':', k.n)) AS binary(4)) AS int) & 2147483647 AS r) AS q
JOIN catalog.Products AS p ON p.Id = 1 + ((o.r2 % 50) + (k.n - 1) * 7) % 50;

UPDATE o SET Subtotal = l.Subtotal
FROM #Order AS o JOIN (SELECT n, SUM(Quantity * UnitPrice) AS Subtotal FROM #Line GROUP BY n) AS l ON l.n = o.n;

/* ------------------------------------------------------------ discounts: ~20% of paid orders, code valid on the order date */
UPDATE o SET DiscountCodeId = pick.Id
FROM #Order AS o
CROSS APPLY (SELECT TOP (1) dc.Id FROM sales.DiscountCodes AS dc
             WHERE o.CreatedAt >= dc.StartsAt AND o.CreatedAt < dc.EndsAt AND o.Subtotal >= dc.MinimumSubtotal
             ORDER BY (dc.Id * 7919 + o.n) % 97) AS pick
WHERE o.Outcome IN ('S', 'R', 'C') AND o.r6 % 100 < 20;

-- Respect usage limits (earliest redemptions win).
WITH ranked AS (SELECT o.DiscountCodeId, ROW_NUMBER() OVER (PARTITION BY o.DiscountCodeId ORDER BY o.CreatedAt, o.n) AS UseNumber, dc.UsageLimit
                FROM #Order AS o JOIN sales.DiscountCodes AS dc ON dc.Id = o.DiscountCodeId)
UPDATE ranked SET DiscountCodeId = NULL WHERE UsageLimit IS NOT NULL AND UseNumber > UsageLimit;

UPDATE o SET DiscountTotal = CASE dc.DiscountKindId
        WHEN 0 THEN CASE WHEN dc.MaximumDiscount IS NOT NULL AND ROUND(o.Subtotal * dc.Value / 100, 2) > dc.MaximumDiscount THEN dc.MaximumDiscount ELSE ROUND(o.Subtotal * dc.Value / 100, 2) END
        ELSE CASE WHEN dc.Value > o.Subtotal THEN o.Subtotal ELSE dc.Value END END
FROM #Order AS o JOIN sales.DiscountCodes AS dc ON dc.Id = o.DiscountCodeId;

UPDATE #Order SET ShippingTotal = CASE WHEN Subtotal - DiscountTotal >= 100 THEN 0 ELSE 8 END;
UPDATE #Order SET Total = Subtotal - DiscountTotal + ShippingTotal;

/* ------------------------------------------------------------ lifecycle timestamps and final statuses */
UPDATE #Order SET CaptureAt = DATEADD(second, CASE Outcome WHEN 'R' THEN 180 + r3 % 420 ELSE 20 END, CreatedAt)
WHERE Outcome IN ('S', 'R', 'C');

UPDATE #Order SET CancelAt = DATEADD(hour, 1 + r4 % 20, CaptureAt) WHERE Outcome = 'C';
UPDATE #Order SET Outcome = 'S', CancelAt = NULL WHERE Outcome = 'C' AND CancelAt > @SeedNow; -- too recent to have been cancelled

UPDATE #Order SET ShipAt = DATEADD(hour, 18 + r4 % 36, CaptureAt) WHERE Outcome IN ('S', 'R');
UPDATE #Order SET DeliverAt = DATEADD(hour, 48 + r5 % 96, ShipAt) WHERE Outcome IN ('S', 'R');
UPDATE #Order SET ShipAt = NULL, DeliverAt = NULL WHERE ShipAt > @SeedNow;
UPDATE #Order SET DeliverAt = NULL WHERE DeliverAt > @SeedNow;

UPDATE #Order SET FailedAttempts = CASE WHEN Outcome = 'F' THEN 1 + r5 % 3 WHEN Outcome = 'R' THEN 1 ELSE 0 END;
UPDATE #Order SET CancelAt = DATEADD(minute, 30 + FailedAttempts * 5, CreatedAt) WHERE Outcome = 'F';

UPDATE #Order SET
    OrderStatusId = CASE WHEN Outcome = 'P' THEN 0 WHEN Outcome IN ('F', 'C') THEN 4
                         WHEN DeliverAt IS NOT NULL THEN 3 WHEN ShipAt IS NOT NULL THEN 2 ELSE 1 END,
    PaymentStatusId = CASE Outcome WHEN 'P' THEN 0 WHEN 'F' THEN 2 ELSE 1 END; -- refunds adjust this below

/* ------------------------------------------------------------ insert orders and items */
INSERT sales.Orders (OrderNumber, CheckoutToken, ConfirmationToken, CustomerId, ContactEmail, RecipientName,
                     ShipLine1, ShipLine2, ShipCity, ShipRegion, ShipPostalCode, ShipCountryCode,
                     Subtotal, DiscountTotal, ShippingTotal, Total, DiscountCodeSnapshot, OrderStatusId, PaymentStatusId, CreatedAt)
SELECT
    CONCAT('NST-', CONVERT(char(6), o.CreatedAt, 12), '-', RIGHT(CONVERT(varchar(10), CONVERT(binary(4), (CAST(o.n AS bigint) * CAST(2654435761 AS bigint)) % CAST(4294967296 AS bigint)), 2), 8)),
    LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT('seed-checkout:', o.n)), 2)),
    LOWER(LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT('seed-confirmation:', o.n)), 2), 48)),
    c.Id, c.Email, a.RecipientName,
    a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.CountryCode,
    o.Subtotal, o.DiscountTotal, o.ShippingTotal, o.Total, dc.Code, o.OrderStatusId, o.PaymentStatusId, o.CreatedAt
FROM #Order AS o
JOIN #Customer AS c ON c.n = o.CustomerN
JOIN #Address AS ak ON ak.n = o.CustomerN AND ak.k = o.AddressK
JOIN customer.Addresses AS a ON a.Id = ak.AddressId
LEFT JOIN sales.DiscountCodes AS dc ON dc.Id = o.DiscountCodeId;

UPDATE o SET OrderId = so.Id
FROM #Order AS o
JOIN sales.Orders AS so ON so.CheckoutToken = LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT('seed-checkout:', o.n)), 2));

INSERT sales.OrderItems (OrderId, ProductId, Sku, ProductName, UnitPrice, Quantity)
SELECT o.OrderId, l.ProductId, l.Sku, l.ProductName, l.UnitPrice, l.Quantity
FROM #Line AS l JOIN #Order AS o ON o.n = l.n;

INSERT sales.DiscountRedemptions (DiscountCodeId, OrderId, RedeemedAt)
SELECT DiscountCodeId, OrderId, CaptureAt FROM #Order WHERE DiscountCodeId IS NOT NULL;

/* ------------------------------------------------------------ payment attempts */
CREATE TABLE #Attempt (n int NOT NULL, AttemptNumber int NOT NULL, StatusId tinyint NOT NULL, DeclineReasonId smallint NULL, AttemptedAt datetimeoffset(7) NOT NULL, PRIMARY KEY (n, AttemptNumber));

-- Failed attempts (F: all of them; R: the first). Card-only reasons are only used for card-like methods,
-- and orders that are later recovered only fail for retryable reasons.
INSERT #Attempt (n, AttemptNumber, StatusId, DeclineReasonId, AttemptedAt)
SELECT o.n, a.n,
       CASE WHEN reason.Id = 8 THEN 3 ELSE 2 END,
       reason.Id,
       DATEADD(second, 20 + (a.n - 1) * 240, o.CreatedAt)
FROM #Order AS o
JOIN #N AS a ON a.n <= o.FailedAttempts
JOIN #Method AS m ON m.n = o.CustomerN AND m.k = o.MethodK
CROSS APPLY (SELECT CAST(CAST(HASHBYTES('SHA2_256', CONCAT('attempt:', o.n, ':', a.n)) AS binary(4)) AS int) & 2147483647 AS r) AS q
CROSS APPLY (SELECT CAST(CASE
        WHEN o.Outcome = 'R' THEN CHOOSE(1 + q.r % 5, 1, 3, 5, 6, 8)
        WHEN m.DetailKind = 'CARD' THEN CHOOSE(1 + q.r % 10, 1, 1, 1, 2, 2, 3, 4, 5, 6, 8)
        ELSE CHOOSE(1 + q.r % 5, 1, 3, 6, 7, 8) END AS smallint) AS Id) AS reason;

-- The successful (or pending) attempt.
INSERT #Attempt (n, AttemptNumber, StatusId, DeclineReasonId, AttemptedAt)
SELECT n, FailedAttempts + 1, CASE WHEN Outcome = 'P' THEN 4 ELSE 1 END, NULL, COALESCE(CaptureAt, DATEADD(second, 20, CreatedAt))
FROM #Order WHERE Outcome IN ('S', 'R', 'C', 'P');

INSERT payment.PaymentAttempts (OrderId, AttemptNumber, PaymentMethodId, Amount, CurrencyCode, PaymentAttemptStatusId, DeclineReasonId, GatewayReference, AttemptedAt)
SELECT o.OrderId, t.AttemptNumber, m.MethodId, o.Total, 'USD', t.StatusId, t.DeclineReasonId,
       CONCAT('ch_', LOWER(LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT('gateway:', o.n, ':', t.AttemptNumber)), 2), 24))),
       t.AttemptedAt
FROM #Attempt AS t
JOIN #Order AS o ON o.n = t.n
JOIN #Method AS m ON m.n = o.CustomerN AND m.k = o.MethodK;

/* ------------------------------------------------------------ status history */
INSERT sales.OrderStatusHistory (OrderId, FromStatusId, ToStatusId, StatusChangeReasonId, ChangedByUserId, ChangedAt, Note)
SELECT o.OrderId, NULL, 0, 1, NULL, o.CreatedAt, NULL FROM #Order AS o;

INSERT sales.OrderStatusHistory (OrderId, FromStatusId, ToStatusId, StatusChangeReasonId, ChangedByUserId, ChangedAt, Note)
SELECT o.OrderId, 0, 1, 2, NULL, o.CaptureAt,
       CASE WHEN o.Outcome = 'R' THEN N'Captured on attempt 2 after the first attempt was declined.' END
FROM #Order AS o WHERE o.CaptureAt IS NOT NULL;

INSERT sales.OrderStatusHistory (OrderId, FromStatusId, ToStatusId, StatusChangeReasonId, ChangedByUserId, ChangedAt, Note)
SELECT o.OrderId, 1, 2, 8, NULL, o.ShipAt, NULL FROM #Order AS o WHERE o.ShipAt IS NOT NULL;

INSERT sales.OrderStatusHistory (OrderId, FromStatusId, ToStatusId, StatusChangeReasonId, ChangedByUserId, ChangedAt, Note)
SELECT o.OrderId, 2, 3, 9, NULL, o.DeliverAt, NULL FROM #Order AS o WHERE o.DeliverAt IS NOT NULL;

-- Failed payments: Pending -> Cancelled, with the final decline documented in the note.
INSERT sales.OrderStatusHistory (OrderId, FromStatusId, ToStatusId, StatusChangeReasonId, ChangedByUserId, ChangedAt, Note)
SELECT o.OrderId, 0, 4, 3, NULL, o.CancelAt,
       CONCAT(N'Payment failed after ', o.FailedAttempts, N' attempt', CASE WHEN o.FailedAttempts > 1 THEN N's' END, N'; last response ', dr.ProcessorCode, N' ', dr.Code, N'.')
FROM #Order AS o
JOIN #Attempt AS last ON last.n = o.n AND last.AttemptNumber = o.FailedAttempts
JOIN ref.DeclineReasons AS dr ON dr.DeclineReasonId = last.DeclineReasonId
WHERE o.Outcome = 'F';

-- Paid then cancelled: Processing -> Cancelled with a business reason.
CREATE TABLE #Cancel (n int PRIMARY KEY, ReasonId smallint NOT NULL);
INSERT #Cancel SELECT n, CASE WHEN r5 % 10 < 6 THEN 4 WHEN r5 % 10 < 8 THEN 5 WHEN r5 % 10 < 9 THEN 7 ELSE 6 END FROM #Order WHERE Outcome = 'C';

INSERT sales.OrderStatusHistory (OrderId, FromStatusId, ToStatusId, StatusChangeReasonId, ChangedByUserId, ChangedAt, Note)
SELECT o.OrderId, 1, 4, x.ReasonId,
       CASE WHEN x.ReasonId = 4 THEN c.Id ELSE @AdminId END,
       o.CancelAt,
       CASE x.ReasonId WHEN 4 THEN N'Customer asked to cancel before dispatch.'
                       WHEN 5 THEN CONCAT(N'Insufficient stock to fulfil ', (SELECT TOP (1) l.Sku FROM #Line AS l WHERE l.n = o.n ORDER BY l.k), N'.')
                       WHEN 7 THEN N'Carrier address validation failed; customer did not respond within 24 hours.'
                       ELSE N'Manual review: billing and shipping details did not match.' END
FROM #Order AS o JOIN #Cancel AS x ON x.n = o.n JOIN #Customer AS c ON c.n = o.CustomerN;

/* ------------------------------------------------------------ cancellation refunds (full amount) */
INSERT payment.Refunds (PaymentAttemptId, RefundReasonId, RefundStatusId, Amount, GatewayReference, RequestedAt, CompletedAt)
SELECT pa.Id, 1,
       CASE WHEN DATEADD(day, 1 + o.r6 % 3, o.CancelAt) <= @SeedNow THEN 2 ELSE 1 END,
       o.Total,
       CONCAT('re_', LOWER(LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT('refund-cancel:', o.n)), 2), 24))),
       o.CancelAt,
       CASE WHEN DATEADD(day, 1 + o.r6 % 3, o.CancelAt) <= @SeedNow THEN DATEADD(day, 1 + o.r6 % 3, o.CancelAt) END
FROM #Order AS o
JOIN payment.PaymentAttempts AS pa ON pa.OrderId = o.OrderId AND pa.PaymentAttemptStatusId = 1
WHERE o.Outcome = 'C';

/* ------------------------------------------------------------ returns on delivered orders (~9%) */
CREATE TABLE #Return
(
    n int PRIMARY KEY, IsFull bit NOT NULL, ReasonId smallint NOT NULL, RequestedAt datetimeoffset(7) NOT NULL,
    ReceivedAt datetimeoffset(7) NULL, StatusId tinyint NOT NULL, ClosedAt datetimeoffset(7) NULL, ReturnId int NULL,
    ReturnedValue decimal(18, 2) NULL, RefundAmount decimal(18, 2) NULL, rr1 int NOT NULL, rr2 int NOT NULL
);
INSERT #Return (n, IsFull, ReasonId, RequestedAt, StatusId, rr1, rr2)
SELECT o.n, CASE WHEN r.r1 % 10 < 4 THEN 1 ELSE 0 END, 1 + r.r2 % 6, DATEADD(hour, 24 + r.r3 % 480, o.DeliverAt), 1, r.r4, r.r5
FROM #Order AS o
CROSS APPLY (SELECT HASHBYTES('SHA2_256', CONCAT('return:', o.n)) AS h) AS x
CROSS APPLY (SELECT
    CAST(SUBSTRING(x.h, 1, 4) AS int) & 2147483647 AS r1,  CAST(SUBSTRING(x.h, 5, 4) AS int) & 2147483647 AS r2,
    CAST(SUBSTRING(x.h, 9, 4) AS int) & 2147483647 AS r3,  CAST(SUBSTRING(x.h, 13, 4) AS int) & 2147483647 AS r4,
    CAST(SUBSTRING(x.h, 17, 4) AS int) & 2147483647 AS r5, CAST(SUBSTRING(x.h, 21, 4) AS int) & 2147483647 AS r6) AS r
WHERE o.DeliverAt IS NOT NULL AND r.r6 % 100 < 9 AND DATEADD(hour, 24 + r.r3 % 480, o.DeliverAt) <= @SeedNow;

UPDATE #Return SET ReceivedAt = DATEADD(hour, 72 + rr1 % 144, RequestedAt)
WHERE DATEADD(hour, 72 + rr1 % 144, RequestedAt) <= @SeedNow;

UPDATE #Return SET
    StatusId = CASE WHEN ReceivedAt IS NULL THEN 1
                    WHEN rr2 % 10 = 0 THEN 4                                  -- rejected after inspection
                    WHEN DATEADD(hour, 24, ReceivedAt) <= @SeedNow THEN 3     -- refunded
                    ELSE 2 END,                                               -- received, refund pending
    ClosedAt = CASE WHEN ReceivedAt IS NULL THEN NULL
                    WHEN rr2 % 10 = 0 THEN DATEADD(hour, 24, ReceivedAt)
                    WHEN DATEADD(hour, 24, ReceivedAt) <= @SeedNow THEN DATEADD(hour, 24, ReceivedAt) END;

INSERT sales.OrderReturns (ReturnNumber, OrderId, ReturnReasonId, ReturnStatusId, RequestedAt, ReceivedAt, ClosedAt, CustomerNote)
SELECT CONCAT('RMA-', CONVERT(char(6), r.RequestedAt, 12), '-', RIGHT(CONCAT('0000', r.n), 5)), o.OrderId, r.ReasonId, r.StatusId,
       r.RequestedAt, r.ReceivedAt, r.ClosedAt,
       CASE r.ReasonId WHEN 1 THEN N'Box was crushed and the item is scratched.' WHEN 2 THEN N'Received a different colour.'
                       WHEN 3 THEN N'Smaller than the photos suggested.' WHEN 5 THEN N'Arrived after the trip.' END
FROM #Return AS r JOIN #Order AS o ON o.n = r.n;

UPDATE r SET ReturnId = rt.Id
FROM #Return AS r JOIN #Order AS o ON o.n = r.n JOIN sales.OrderReturns AS rt ON rt.OrderId = o.OrderId;

-- Full return: every line, full quantity. Partial: one unit of the first line.
INSERT sales.OrderReturnItems (OrderReturnId, OrderItemId, OrderId, Quantity)
SELECT r.ReturnId, oi.Id, o.OrderId, CASE WHEN r.IsFull = 1 THEN l.Quantity ELSE 1 END
FROM #Return AS r
JOIN #Order AS o ON o.n = r.n
JOIN #Line AS l ON l.n = r.n AND (r.IsFull = 1 OR l.k = 1)
JOIN sales.OrderItems AS oi ON oi.OrderId = o.OrderId AND oi.ProductId = l.ProductId;

-- Refund the returned goods net of the order's discount (shipping is not refunded).
UPDATE r SET
    ReturnedValue = v.Value,
    RefundAmount = CASE WHEN o.Subtotal = 0 THEN 0 ELSE ROUND(v.Value * (o.Subtotal - o.DiscountTotal) / o.Subtotal, 2) END
FROM #Return AS r
JOIN #Order AS o ON o.n = r.n
CROSS APPLY (SELECT SUM(ri.Quantity * oi.UnitPrice) AS Value FROM sales.OrderReturnItems AS ri JOIN sales.OrderItems AS oi ON oi.Id = ri.OrderItemId WHERE ri.OrderReturnId = r.ReturnId) AS v;

INSERT payment.Refunds (PaymentAttemptId, OrderReturnId, RefundReasonId, RefundStatusId, Amount, GatewayReference, RequestedAt, CompletedAt)
SELECT pa.Id, r.ReturnId, 2, CASE WHEN r.StatusId = 3 THEN 2 ELSE 1 END, r.RefundAmount,
       CONCAT('re_', LOWER(LEFT(CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT('refund-return:', r.n)), 2), 24))),
       r.ReceivedAt, CASE WHEN r.StatusId = 3 THEN r.ClosedAt END
FROM #Return AS r
JOIN #Order AS o ON o.n = r.n
JOIN payment.PaymentAttempts AS pa ON pa.OrderId = o.OrderId AND pa.PaymentAttemptStatusId = 1
WHERE r.StatusId IN (2, 3) AND r.RefundAmount > 0;

/* ------------------------------------------------------------ order payment status reflects completed refunds */
UPDATE so SET PaymentStatusId = CASE WHEN refunded.Amount >= so.Total THEN 3 ELSE 4 END
FROM sales.Orders AS so
JOIN #Order AS o ON o.OrderId = so.Id
CROSS APPLY (SELECT SUM(rf.Amount) AS Amount FROM payment.Refunds AS rf JOIN payment.PaymentAttempts AS pa ON pa.Id = rf.PaymentAttemptId
             WHERE pa.OrderId = so.Id AND rf.RefundStatusId = 2) AS refunded
WHERE refunded.Amount > 0;

DROP TABLE #Cancel, #Return, #Attempt, #Line, #Order, #Method, #Address, #Customer, #N, #First, #Last, #Street, #City, #Bank;

COMMIT TRANSACTION;
