/*
    views.sql
    Read models for reporting, customer service and BI. Re-applied on every deploy
    (CREATE OR ALTER), so editing this file and redeploying is enough.
    The application role can SELECT from these; the reporting role can ONLY read these.
*/
SET NOCOUNT ON;
GO

/* One row per order with names resolved and payment/refund money rolled up. */
CREATE OR ALTER VIEW reporting.vw_OrderSummary
AS
SELECT
    o.Id                    AS OrderId,
    o.OrderNumber,
    o.CreatedAt,
    o.CustomerId,
    u.DisplayName           AS CustomerName,
    o.ContactEmail,
    CASE WHEN o.CustomerId IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsGuest,
    os.Name                 AS OrderStatus,
    ps.Name                 AS PaymentStatus,
    items.LineCount,
    items.UnitCount,
    o.Subtotal,
    o.DiscountTotal,
    o.DiscountCodeSnapshot  AS DiscountCode,
    o.ShippingTotal,
    o.Total,
    ISNULL(pay.CapturedAmount, 0)                                 AS CapturedAmount,
    ISNULL(pay.AttemptCount, 0)                                   AS PaymentAttempts,
    ISNULL(pay.FailedAttemptCount, 0)                             AS FailedPaymentAttempts,
    ISNULL(rfd.RefundedAmount, 0)                                 AS RefundedAmount,
    ISNULL(pay.CapturedAmount, 0) - ISNULL(rfd.RefundedAmount, 0) AS NetRevenue,
    o.ShipCity,
    o.ShipRegion,
    o.ShipCountryCode
FROM sales.Orders AS o
JOIN ref.OrderStatuses AS os ON os.OrderStatusId = o.OrderStatusId
JOIN ref.PaymentStatuses AS ps ON ps.PaymentStatusId = o.PaymentStatusId
LEFT JOIN auth.Users AS u ON u.Id = o.CustomerId
CROSS APPLY (SELECT COUNT(*) AS LineCount, SUM(oi.Quantity) AS UnitCount FROM sales.OrderItems AS oi WHERE oi.OrderId = o.Id) AS items
OUTER APPLY
(
    SELECT
        SUM(CASE WHEN pa.PaymentAttemptStatusId = 1 THEN pa.Amount END) AS CapturedAmount,
        COUNT(*) AS AttemptCount,
        SUM(CASE WHEN pas.IsFailure = 1 THEN 1 ELSE 0 END) AS FailedAttemptCount
    FROM payment.PaymentAttempts AS pa
    JOIN ref.PaymentAttemptStatuses AS pas ON pas.PaymentAttemptStatusId = pa.PaymentAttemptStatusId
    WHERE pa.OrderId = o.Id
) AS pay
OUTER APPLY
(
    SELECT SUM(r.Amount) AS RefundedAmount
    FROM payment.Refunds AS r
    JOIN payment.PaymentAttempts AS pa ON pa.Id = r.PaymentAttemptId
    WHERE pa.OrderId = o.Id AND r.RefundStatusId = 2
) AS rfd;
GO

/* Every declined or errored payment attempt, with the documented reason and whether the
   order was rescued by a later successful attempt. */
CREATE OR ALTER VIEW reporting.vw_FailedPayments
AS
SELECT
    pa.Id                AS PaymentAttemptId,
    o.Id                 AS OrderId,
    o.OrderNumber,
    o.CustomerId,
    o.ContactEmail,
    pa.AttemptNumber,
    pa.AttemptedAt,
    pa.Amount,
    pa.CurrencyCode,
    pas.Name             AS AttemptStatus,
    dr.Code              AS DeclineCode,
    dr.ProcessorCode,
    dr.Description       AS DeclineDescription,
    dr.IsRetryable,
    pmt.Name             AS PaymentMethodType,
    pa.GatewayReference,
    CASE WHEN EXISTS (SELECT 1 FROM payment.PaymentAttempts AS later
                      WHERE later.OrderId = pa.OrderId AND later.PaymentAttemptStatusId = 1 AND later.AttemptNumber > pa.AttemptNumber)
         THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS RecoveredByLaterAttempt,
    os.Name              AS CurrentOrderStatus
FROM payment.PaymentAttempts AS pa
JOIN ref.PaymentAttemptStatuses AS pas ON pas.PaymentAttemptStatusId = pa.PaymentAttemptStatusId AND pas.IsFailure = 1
JOIN ref.DeclineReasons AS dr ON dr.DeclineReasonId = pa.DeclineReasonId
JOIN sales.Orders AS o ON o.Id = pa.OrderId
JOIN ref.OrderStatuses AS os ON os.OrderStatusId = o.OrderStatusId
LEFT JOIN payment.PaymentMethods AS pm ON pm.Id = pa.PaymentMethodId
LEFT JOIN ref.PaymentMethodTypes AS pmt ON pmt.PaymentMethodTypeId = pm.PaymentMethodTypeId;
GO

/* Full status timeline per order, oldest first when sorted by ChangedAt. */
CREATE OR ALTER VIEW reporting.vw_OrderTimeline
AS
SELECT
    h.Id            AS HistoryId,
    h.OrderId,
    o.OrderNumber,
    h.ChangedAt,
    fs.Name         AS FromStatus,
    ts.Name         AS ToStatus,
    r.Code          AS ReasonCode,
    r.Description   AS Reason,
    h.Note,
    h.ChangedByUserId,
    cu.Email        AS ChangedByEmail
FROM sales.OrderStatusHistory AS h
JOIN sales.Orders AS o ON o.Id = h.OrderId
LEFT JOIN ref.OrderStatuses AS fs ON fs.OrderStatusId = h.FromStatusId
JOIN ref.OrderStatuses AS ts ON ts.OrderStatusId = h.ToStatusId
LEFT JOIN ref.StatusChangeReasons AS r ON r.StatusChangeReasonId = h.StatusChangeReasonId
LEFT JOIN auth.Users AS cu ON cu.Id = h.ChangedByUserId;
GO

/* Returns with their items' value and the refund (if any) that settled them. */
CREATE OR ALTER VIEW reporting.vw_ReturnsAndRefunds
AS
SELECT
    rt.Id             AS OrderReturnId,
    rt.ReturnNumber,
    o.Id              AS OrderId,
    o.OrderNumber,
    o.CustomerId,
    rr.Code           AS ReturnReason,
    rs.Name           AS ReturnStatus,
    rt.RequestedAt,
    rt.ReceivedAt,
    rt.ClosedAt,
    items.ItemsReturned,
    items.ReturnedValue,
    rf.Amount         AS RefundAmount,
    rfs.Name          AS RefundStatus,
    rf.CompletedAt    AS RefundCompletedAt
FROM sales.OrderReturns AS rt
JOIN sales.Orders AS o ON o.Id = rt.OrderId
JOIN ref.ReturnReasons AS rr ON rr.ReturnReasonId = rt.ReturnReasonId
JOIN ref.ReturnStatuses AS rs ON rs.ReturnStatusId = rt.ReturnStatusId
CROSS APPLY
(
    SELECT SUM(ri.Quantity) AS ItemsReturned, SUM(CAST(ri.Quantity * oi.UnitPrice AS decimal(18, 2))) AS ReturnedValue
    FROM sales.OrderReturnItems AS ri
    JOIN sales.OrderItems AS oi ON oi.Id = ri.OrderItemId
    WHERE ri.OrderReturnId = rt.Id
) AS items
LEFT JOIN payment.Refunds AS rf ON rf.OrderReturnId = rt.Id
LEFT JOIN ref.RefundStatuses AS rfs ON rfs.RefundStatusId = rf.RefundStatusId;
GO

/* Per-customer lifetime value. Successful = payment captured (even if later refunded). */
CREATE OR ALTER VIEW reporting.vw_CustomerLifetimeValue
AS
SELECT
    u.Id               AS CustomerId,
    u.DisplayName,
    u.Email,
    u.CreatedAt        AS CustomerSince,
    COUNT(s.OrderId)                                         AS OrdersPlaced,
    SUM(CASE WHEN s.CapturedAmount > 0 THEN 1 ELSE 0 END)    AS SuccessfulOrders,
    SUM(CASE WHEN s.PaymentStatus = N'Failed' THEN 1 ELSE 0 END) AS FailedOrders,
    ISNULL(SUM(s.CapturedAmount), 0)                         AS GrossSpend,
    ISNULL(SUM(s.RefundedAmount), 0)                         AS Refunded,
    ISNULL(SUM(s.NetRevenue), 0)                             AS NetSpend,
    CAST(ISNULL(SUM(s.CapturedAmount) / NULLIF(SUM(CASE WHEN s.CapturedAmount > 0 THEN 1 ELSE 0 END), 0), 0) AS decimal(18, 2)) AS AverageOrderValue,
    MIN(s.CreatedAt)   AS FirstOrderAt,
    MAX(s.CreatedAt)   AS LastOrderAt
FROM auth.Users AS u
LEFT JOIN reporting.vw_OrderSummary AS s ON s.CustomerId = u.Id
GROUP BY u.Id, u.DisplayName, u.Email, u.CreatedAt;
GO

/* Units and revenue per product from paid, non-cancelled orders, with returns. */
CREATE OR ALTER VIEW reporting.vw_ProductSalesPerformance
AS
SELECT
    p.Id            AS ProductId,
    p.Sku,
    p.Name,
    c.Name          AS Category,
    p.Price         AS CurrentPrice,
    p.StockQuantity AS CurrentStock,
    ISNULL(sold.Orders, 0)       AS Orders,
    ISNULL(sold.UnitsSold, 0)    AS UnitsSold,
    ISNULL(sold.Revenue, 0)      AS Revenue,
    ISNULL(ret.UnitsReturned, 0) AS UnitsReturned,
    CAST(ISNULL(100.0 * ret.UnitsReturned / NULLIF(sold.UnitsSold, 0), 0) AS decimal(5, 2)) AS ReturnRatePercent
FROM catalog.Products AS p
JOIN catalog.Categories AS c ON c.Id = p.CategoryId
OUTER APPLY
(
    SELECT COUNT(DISTINCT oi.OrderId) AS Orders, SUM(oi.Quantity) AS UnitsSold, SUM(oi.LineTotal) AS Revenue
    FROM sales.OrderItems AS oi
    JOIN sales.Orders AS o ON o.Id = oi.OrderId
    WHERE oi.ProductId = p.Id AND o.PaymentStatusId IN (1, 3, 4) AND o.OrderStatusId <> 4
) AS sold
OUTER APPLY
(
    SELECT SUM(ri.Quantity) AS UnitsReturned
    FROM sales.OrderReturnItems AS ri
    JOIN sales.OrderItems AS oi ON oi.Id = ri.OrderItemId
    JOIN sales.OrderReturns AS rt ON rt.Id = ri.OrderReturnId
    WHERE oi.ProductId = p.Id AND rt.ReturnStatusId IN (2, 3)
) AS ret;
GO

/* Daily totals (UTC days). */
CREATE OR ALTER VIEW reporting.vw_DailySales
AS
SELECT
    CAST(SWITCHOFFSET(s.CreatedAt, '+00:00') AS date)                 AS SalesDate,
    COUNT(*)                                                          AS OrdersPlaced,
    SUM(CASE WHEN s.CapturedAmount > 0 THEN 1 ELSE 0 END)             AS PaidOrders,
    SUM(CASE WHEN s.PaymentStatus = N'Failed' THEN 1 ELSE 0 END)      AS FailedOrders,
    SUM(CASE WHEN s.OrderStatus = N'Cancelled' THEN 1 ELSE 0 END)     AS CancelledOrders,
    SUM(s.CapturedAmount)                                             AS GrossRevenue,
    SUM(s.RefundedAmount)                                             AS Refunds,
    SUM(s.NetRevenue)                                                 AS NetRevenue
FROM reporting.vw_OrderSummary AS s
GROUP BY CAST(SWITCHOFFSET(s.CreatedAt, '+00:00') AS date);
GO

/* Stored payment methods as customers see them: masked, one display line each. */
CREATE OR ALTER VIEW reporting.vw_CustomerPaymentMethods
AS
SELECT
    pm.Id          AS PaymentMethodId,
    pm.UserId,
    u.Email,
    t.Code         AS MethodType,
    CASE t.DetailKind
        WHEN 'CARD'   THEN CONCAT(t.Name, N' · ', cb.Name, N' •••• ', cd.Last4, N' · exp ', RIGHT(CONCAT('0', cd.ExpMonth), 2), N'/', cd.ExpYear)
        WHEN 'WALLET' THEN CONCAT(t.Name, N' · ', wd.AccountEmailMasked)
        WHEN 'BANK'   THEN CONCAT(bd.BankName, N' ', LOWER(bd.AccountType), N' •••• ', bd.AccountLast4)
    END            AS DisplayName,
    pm.IsDefault,
    CASE WHEN cd.PaymentMethodId IS NOT NULL AND DATEFROMPARTS(cd.ExpYear, cd.ExpMonth, 1) < DATEFROMPARTS(YEAR(SYSDATETIME()), MONTH(SYSDATETIME()), 1)
         THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsExpired,
    CASE WHEN pm.RemovedAt IS NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS IsActive,
    a.City         AS BillingCity,
    a.Region       AS BillingRegion,
    pm.CreatedAt,
    pm.RemovedAt
FROM payment.PaymentMethods AS pm
JOIN auth.Users AS u ON u.Id = pm.UserId
JOIN ref.PaymentMethodTypes AS t ON t.PaymentMethodTypeId = pm.PaymentMethodTypeId
LEFT JOIN payment.CardDetails AS cd ON cd.PaymentMethodId = pm.Id
LEFT JOIN ref.CardBrands AS cb ON cb.CardBrandId = cd.CardBrandId
LEFT JOIN payment.WalletDetails AS wd ON wd.PaymentMethodId = pm.Id
LEFT JOIN payment.BankAccountDetails AS bd ON bd.PaymentMethodId = pm.Id
LEFT JOIN customer.Addresses AS a ON a.Id = pm.BillingAddressId;
GO
