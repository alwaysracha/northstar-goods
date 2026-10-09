/*
    0002_core_tables.sql
    Identity, catalog, customer, sales and payment tables.

    Design notes
    - Normalised to 3NF. Deliberate exceptions are order "snapshots" (item name/SKU/price,
      shipping address, contact email, discount code text, money totals): an order is a
      legal record of what was agreed at purchase time, so it must not change when the
      product, address or discount is later edited.
    - Derived money values are guarded by CHECK constraints (Orders.Total) or computed
      columns (OrderItems.LineTotal) so they can never disagree with their inputs.
    - Subtype tables (CardDetails / WalletDetails / BankAccountDetails) use a composite
      foreign key on (PaymentMethodId, PaymentMethodTypeId) so a detail row can only attach
      to a payment method of the matching type.
    - Financial history (orders, attempts, refunds, status history) never cascades on delete.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;
GO

/* ---------------------------------------------------------------- auth (ASP.NET Core Identity) */
CREATE TABLE auth.Users
(
    Id                   nvarchar(64)      NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    DisplayName          nvarchar(100)     NOT NULL,
    CreatedAt            datetimeoffset(7) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UserName             nvarchar(256)     NULL,
    NormalizedUserName   nvarchar(256)     NULL,
    Email                nvarchar(256)     NULL,
    NormalizedEmail      nvarchar(256)     NULL,
    EmailConfirmed       bit               NOT NULL CONSTRAINT DF_Users_EmailConfirmed DEFAULT 0,
    PasswordHash         nvarchar(max)     NULL,
    SecurityStamp        nvarchar(max)     NULL,
    ConcurrencyStamp     nvarchar(max)     NULL,
    PhoneNumber          nvarchar(32)      NULL,
    PhoneNumberConfirmed bit               NOT NULL CONSTRAINT DF_Users_PhoneNumberConfirmed DEFAULT 0,
    TwoFactorEnabled     bit               NOT NULL CONSTRAINT DF_Users_TwoFactorEnabled DEFAULT 0,
    LockoutEnd           datetimeoffset(7) NULL,
    LockoutEnabled       bit               NOT NULL CONSTRAINT DF_Users_LockoutEnabled DEFAULT 1,
    AccessFailedCount    int               NOT NULL CONSTRAINT DF_Users_AccessFailedCount DEFAULT 0,
    CONSTRAINT CK_Users_AccessFailedCount CHECK (AccessFailedCount >= 0)
);
CREATE UNIQUE INDEX UserNameIndex ON auth.Users (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
CREATE UNIQUE INDEX EmailIndex    ON auth.Users (NormalizedEmail)    WHERE NormalizedEmail IS NOT NULL;

CREATE TABLE auth.Roles
(
    Id               nvarchar(64)  NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Name             nvarchar(256) NULL,
    NormalizedName   nvarchar(256) NULL,
    ConcurrencyStamp nvarchar(max) NULL
);
CREATE UNIQUE INDEX RoleNameIndex ON auth.Roles (NormalizedName) WHERE NormalizedName IS NOT NULL;

CREATE TABLE auth.UserRoles
(
    UserId nvarchar(64) NOT NULL CONSTRAINT FK_UserRoles_Users REFERENCES auth.Users (Id) ON DELETE CASCADE,
    RoleId nvarchar(64) NOT NULL CONSTRAINT FK_UserRoles_Roles REFERENCES auth.Roles (Id) ON DELETE CASCADE,
    CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId)
);
CREATE INDEX IX_UserRoles_RoleId ON auth.UserRoles (RoleId);

CREATE TABLE auth.UserClaims
(
    Id         int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_UserClaims PRIMARY KEY,
    UserId     nvarchar(64)  NOT NULL CONSTRAINT FK_UserClaims_Users REFERENCES auth.Users (Id) ON DELETE CASCADE,
    ClaimType  nvarchar(max) NULL,
    ClaimValue nvarchar(max) NULL
);
CREATE INDEX IX_UserClaims_UserId ON auth.UserClaims (UserId);

CREATE TABLE auth.RoleClaims
(
    Id         int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_RoleClaims PRIMARY KEY,
    RoleId     nvarchar(64)  NOT NULL CONSTRAINT FK_RoleClaims_Roles REFERENCES auth.Roles (Id) ON DELETE CASCADE,
    ClaimType  nvarchar(max) NULL,
    ClaimValue nvarchar(max) NULL
);
CREATE INDEX IX_RoleClaims_RoleId ON auth.RoleClaims (RoleId);

CREATE TABLE auth.UserLogins
(
    LoginProvider       nvarchar(128) NOT NULL,
    ProviderKey         nvarchar(128) NOT NULL,
    ProviderDisplayName nvarchar(max) NULL,
    UserId              nvarchar(64)  NOT NULL CONSTRAINT FK_UserLogins_Users REFERENCES auth.Users (Id) ON DELETE CASCADE,
    CONSTRAINT PK_UserLogins PRIMARY KEY (LoginProvider, ProviderKey)
);
CREATE INDEX IX_UserLogins_UserId ON auth.UserLogins (UserId);

CREATE TABLE auth.UserTokens
(
    UserId        nvarchar(64)  NOT NULL CONSTRAINT FK_UserTokens_Users REFERENCES auth.Users (Id) ON DELETE CASCADE,
    LoginProvider nvarchar(128) NOT NULL,
    Name          nvarchar(128) NOT NULL,
    Value         nvarchar(max) NULL,
    CONSTRAINT PK_UserTokens PRIMARY KEY (UserId, LoginProvider, Name)
);
GO

/* ---------------------------------------------------------------- catalog */
CREATE TABLE catalog.Categories
(
    Id           int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name         nvarchar(80)  NOT NULL,
    Slug         varchar(80)   NOT NULL CONSTRAINT UQ_Categories_Slug UNIQUE,
    Description  nvarchar(500) NOT NULL CONSTRAINT DF_Categories_Description DEFAULT N'',
    ImagePath    nvarchar(260) NOT NULL CONSTRAINT DF_Categories_ImagePath DEFAULT N'',
    DisplayOrder int           NOT NULL CONSTRAINT DF_Categories_DisplayOrder DEFAULT 0,
    IsActive     bit           NOT NULL CONSTRAINT DF_Categories_IsActive DEFAULT 1,
    CONSTRAINT CK_Categories_Slug CHECK (Slug NOT LIKE '%[^a-z0-9-]%' COLLATE Latin1_General_100_BIN2),
    CONSTRAINT CK_Categories_DisplayOrder CHECK (DisplayOrder >= 0)
);

CREATE TABLE catalog.Products
(
    Id                 int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    CategoryId         int               NOT NULL CONSTRAINT FK_Products_Categories REFERENCES catalog.Categories (Id),
    Name               nvarchar(120)     NOT NULL,
    Slug               varchar(120)      NOT NULL CONSTRAINT UQ_Products_Slug UNIQUE,
    Sku                varchar(40)       NOT NULL CONSTRAINT UQ_Products_Sku UNIQUE,
    ShortDescription   nvarchar(240)     NOT NULL CONSTRAINT DF_Products_ShortDescription DEFAULT N'',
    Description        nvarchar(4000)    NOT NULL CONSTRAINT DF_Products_Description DEFAULT N'',
    Price              decimal(18, 2)    NOT NULL,
    StockQuantity      int               NOT NULL,
    ImagePath          nvarchar(260)     NOT NULL CONSTRAINT DF_Products_ImagePath DEFAULT N'',
    SecondaryImagePath nvarchar(260)     NULL,
    IsFeatured         bit               NOT NULL CONSTRAINT DF_Products_IsFeatured DEFAULT 0,
    IsActive           bit               NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1,
    CreatedAt          datetimeoffset(7) NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt          datetimeoffset(7) NOT NULL CONSTRAINT DF_Products_UpdatedAt DEFAULT SYSDATETIMEOFFSET(),
    StockVersion       bigint            NOT NULL CONSTRAINT DF_Products_StockVersion DEFAULT 0, -- optimistic concurrency token
    CONSTRAINT CK_Product_Price CHECK (Price >= 0),
    CONSTRAINT CK_Product_Stock CHECK (StockQuantity >= 0),
    CONSTRAINT CK_Product_Slug CHECK (Slug NOT LIKE '%[^a-z0-9-]%' COLLATE Latin1_General_100_BIN2),
    CONSTRAINT CK_Product_Dates CHECK (UpdatedAt >= CreatedAt)
);
CREATE INDEX IX_Products_CategoryId ON catalog.Products (CategoryId);
CREATE INDEX IX_Products_IsActive_CategoryId_Price ON catalog.Products (IsActive, CategoryId, Price);
GO

/* ---------------------------------------------------------------- customer */
CREATE TABLE customer.Addresses
(
    Id            int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Addresses PRIMARY KEY,
    UserId        nvarchar(64)      NOT NULL CONSTRAINT FK_Addresses_Users REFERENCES auth.Users (Id) ON DELETE CASCADE,
    Label         nvarchar(40)      NOT NULL CONSTRAINT DF_Addresses_Label DEFAULT N'Home',
    RecipientName nvarchar(100)     NOT NULL,
    Line1         nvarchar(150)     NOT NULL,
    Line2         nvarchar(150)     NULL,
    City          nvarchar(80)      NOT NULL,
    Region        nvarchar(80)      NOT NULL,
    PostalCode    nvarchar(20)      NOT NULL,
    CountryCode   char(2)           NOT NULL CONSTRAINT FK_Addresses_Countries REFERENCES ref.Countries (CountryCode),
    CreatedAt     datetimeoffset(7) NOT NULL CONSTRAINT DF_Addresses_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    -- Lets other tables require "an address that belongs to this same user".
    CONSTRAINT UQ_Addresses_Id_UserId UNIQUE (Id, UserId)
);
CREATE INDEX IX_Addresses_UserId ON customer.Addresses (UserId);
GO

/* ---------------------------------------------------------------- sales: carts and discounts */
CREATE TABLE sales.DiscountCodes
(
    Id              int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_DiscountCodes PRIMARY KEY,
    Code            nvarchar(64)      NOT NULL,
    NormalizedCode  nvarchar(64)      NOT NULL,
    DiscountKindId  tinyint           NOT NULL CONSTRAINT FK_DiscountCodes_DiscountKinds REFERENCES ref.DiscountKinds (DiscountKindId),
    Value           decimal(18, 2)    NOT NULL,
    MinimumSubtotal decimal(18, 2)    NOT NULL CONSTRAINT DF_DiscountCodes_MinimumSubtotal DEFAULT 0,
    MaximumDiscount decimal(18, 2)    NULL,
    StartsAt        datetimeoffset(7) NOT NULL,
    EndsAt          datetimeoffset(7) NOT NULL,
    IsActive        bit               NOT NULL CONSTRAINT DF_DiscountCodes_IsActive DEFAULT 1,
    UsageLimit      int               NULL,
    CONSTRAINT UQ_DiscountCodes_NormalizedCode UNIQUE (NormalizedCode),
    -- The normalised code is always the trimmed, upper-cased code (binary comparison, so case matters).
    CONSTRAINT CK_DiscountCode_Normalized CHECK (NormalizedCode COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM(Code))) COLLATE Latin1_General_100_BIN2),
    CONSTRAINT CK_DiscountCode_Value CHECK (Value > 0),
    CONSTRAINT CK_DiscountCode_Percentage CHECK (DiscountKindId <> 0 OR Value <= 100),
    CONSTRAINT CK_DiscountCode_MinimumSubtotal CHECK (MinimumSubtotal >= 0),
    CONSTRAINT CK_DiscountCode_MaximumDiscount CHECK (MaximumDiscount IS NULL OR MaximumDiscount > 0),
    CONSTRAINT CK_DiscountCode_Window CHECK (EndsAt > StartsAt),
    CONSTRAINT CK_DiscountCode_UsageLimit CHECK (UsageLimit IS NULL OR UsageLimit > 0)
);

CREATE TABLE sales.Carts
(
    Id           uniqueidentifier  NOT NULL CONSTRAINT PK_Carts PRIMARY KEY NONCLUSTERED,
    UserId       nvarchar(64)      NULL CONSTRAINT FK_Carts_Users REFERENCES auth.Users (Id) ON DELETE CASCADE,
    SessionKey   varchar(64)       NULL,
    DiscountCode nvarchar(64)      NULL, -- code the shopper typed; validated at display and checkout
    UpdatedAt    datetimeoffset(7) NOT NULL CONSTRAINT DF_Carts_UpdatedAt DEFAULT SYSDATETIMEOFFSET(),
    -- A cart belongs to exactly one owner: a signed-in user or an anonymous browser session.
    CONSTRAINT CK_Carts_Owner CHECK ((UserId IS NULL AND SessionKey IS NOT NULL) OR (UserId IS NOT NULL AND SessionKey IS NULL))
);
CREATE UNIQUE INDEX IX_Carts_UserId     ON sales.Carts (UserId)     WHERE UserId IS NOT NULL;
CREATE UNIQUE INDEX IX_Carts_SessionKey ON sales.Carts (SessionKey) WHERE SessionKey IS NOT NULL;
CREATE INDEX IX_Carts_UpdatedAt ON sales.Carts (UpdatedAt); -- abandoned-cart clean-up

CREATE TABLE sales.CartItems
(
    Id        int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_CartItems PRIMARY KEY,
    CartId    uniqueidentifier NOT NULL CONSTRAINT FK_CartItems_Carts REFERENCES sales.Carts (Id) ON DELETE CASCADE,
    ProductId int              NOT NULL CONSTRAINT FK_CartItems_Products REFERENCES catalog.Products (Id) ON DELETE CASCADE,
    Quantity  int              NOT NULL,
    CONSTRAINT UQ_CartItems_CartId_ProductId UNIQUE (CartId, ProductId),
    CONSTRAINT CK_CartItem_Quantity CHECK (Quantity > 0)
);
CREATE INDEX IX_CartItems_ProductId ON sales.CartItems (ProductId);
GO

/* ---------------------------------------------------------------- sales: orders */
CREATE TABLE sales.Orders
(
    Id                   int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    OrderNumber          varchar(64)       NOT NULL CONSTRAINT UQ_Orders_OrderNumber UNIQUE,
    CheckoutToken        varchar(128)      NOT NULL CONSTRAINT UQ_Orders_CheckoutToken UNIQUE,     -- idempotency key
    ConfirmationToken    varchar(64)       NOT NULL CONSTRAINT UQ_Orders_ConfirmationToken UNIQUE, -- guest receipt link
    CustomerId           nvarchar(64)      NULL CONSTRAINT FK_Orders_Users REFERENCES auth.Users (Id), -- NULL = guest checkout
    ContactEmail         nvarchar(256)     NOT NULL,
    RecipientName        nvarchar(100)     NOT NULL,
    ShipLine1            nvarchar(150)     NOT NULL,
    ShipLine2            nvarchar(150)     NULL,
    ShipCity             nvarchar(80)      NOT NULL,
    ShipRegion           nvarchar(80)      NOT NULL,
    ShipPostalCode       nvarchar(20)      NOT NULL,
    ShipCountryCode      char(2)           NOT NULL CONSTRAINT FK_Orders_Countries REFERENCES ref.Countries (CountryCode),
    Subtotal             decimal(18, 2)    NOT NULL,
    DiscountTotal        decimal(18, 2)    NOT NULL CONSTRAINT DF_Orders_DiscountTotal DEFAULT 0,
    ShippingTotal        decimal(18, 2)    NOT NULL CONSTRAINT DF_Orders_ShippingTotal DEFAULT 0,
    Total                decimal(18, 2)    NOT NULL,
    DiscountCodeSnapshot nvarchar(64)      NULL,
    OrderStatusId        tinyint           NOT NULL CONSTRAINT FK_Orders_OrderStatuses REFERENCES ref.OrderStatuses (OrderStatusId),
    PaymentStatusId      tinyint           NOT NULL CONSTRAINT FK_Orders_PaymentStatuses REFERENCES ref.PaymentStatuses (PaymentStatusId),
    CreatedAt            datetimeoffset(7) NOT NULL CONSTRAINT DF_Orders_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT CK_Orders_Amounts CHECK (Subtotal >= 0 AND DiscountTotal >= 0 AND ShippingTotal >= 0 AND DiscountTotal <= Subtotal),
    CONSTRAINT CK_Orders_Total CHECK (Total = Subtotal - DiscountTotal + ShippingTotal),
    -- A failed payment can only leave the order pending or cancelled.
    CONSTRAINT CK_Orders_FailedPayment CHECK (PaymentStatusId <> 2 OR OrderStatusId IN (0, 4)),
    -- Nothing ships unless it was paid.
    CONSTRAINT CK_Orders_ShippedIsPaid CHECK (OrderStatusId NOT IN (2, 3) OR PaymentStatusId IN (1, 3, 4))
);
CREATE INDEX IX_Orders_CustomerId_CreatedAt ON sales.Orders (CustomerId, CreatedAt);
CREATE INDEX IX_Orders_OrderStatusId_CreatedAt ON sales.Orders (OrderStatusId, CreatedAt);
CREATE INDEX IX_Orders_PaymentStatusId ON sales.Orders (PaymentStatusId);
CREATE INDEX IX_Orders_CreatedAt ON sales.Orders (CreatedAt) INCLUDE (Total, OrderStatusId, PaymentStatusId);

CREATE TABLE sales.OrderItems
(
    Id          int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId     int            NOT NULL CONSTRAINT FK_OrderItems_Orders REFERENCES sales.Orders (Id),
    ProductId   int            NULL CONSTRAINT FK_OrderItems_Products REFERENCES catalog.Products (Id) ON DELETE SET NULL,
    Sku         varchar(40)    NOT NULL,      -- snapshot at purchase
    ProductName nvarchar(120)  NOT NULL,      -- snapshot at purchase
    UnitPrice   decimal(18, 2) NOT NULL,      -- snapshot at purchase
    Quantity    int            NOT NULL,
    LineTotal   AS CAST(UnitPrice * Quantity AS decimal(18, 2)) PERSISTED,
    CONSTRAINT UQ_OrderItems_Id_OrderId UNIQUE (Id, OrderId),
    CONSTRAINT CK_OrderItem_Quantity CHECK (Quantity > 0),
    CONSTRAINT CK_OrderItem_UnitPrice CHECK (UnitPrice >= 0)
);
CREATE INDEX IX_OrderItems_OrderId ON sales.OrderItems (OrderId);
CREATE INDEX IX_OrderItems_ProductId ON sales.OrderItems (ProductId);
CREATE UNIQUE INDEX UQ_OrderItems_OrderId_ProductId ON sales.OrderItems (OrderId, ProductId) WHERE ProductId IS NOT NULL;

CREATE TABLE sales.DiscountRedemptions
(
    Id             int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_DiscountRedemptions PRIMARY KEY,
    DiscountCodeId int               NOT NULL CONSTRAINT FK_DiscountRedemptions_DiscountCodes REFERENCES sales.DiscountCodes (Id),
    OrderId        int               NOT NULL CONSTRAINT FK_DiscountRedemptions_Orders REFERENCES sales.Orders (Id),
    RedeemedAt     datetimeoffset(7) NOT NULL CONSTRAINT DF_DiscountRedemptions_RedeemedAt DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT UQ_DiscountRedemptions_OrderId UNIQUE (OrderId) -- one code per order
);
CREATE INDEX IX_DiscountRedemptions_DiscountCodeId ON sales.DiscountRedemptions (DiscountCodeId);

-- Audit trail of every order status change. The composite FK checks each change against
-- ref.OrderStatusTransitions; the first row of an order has FromStatusId NULL, so it is exempt.
CREATE TABLE sales.OrderStatusHistory
(
    Id                   bigint IDENTITY(1, 1) NOT NULL CONSTRAINT PK_OrderStatusHistory PRIMARY KEY,
    OrderId              int               NOT NULL CONSTRAINT FK_OrderStatusHistory_Orders REFERENCES sales.Orders (Id),
    FromStatusId         tinyint           NULL CONSTRAINT FK_OrderStatusHistory_FromStatus REFERENCES ref.OrderStatuses (OrderStatusId),
    ToStatusId           tinyint           NOT NULL CONSTRAINT FK_OrderStatusHistory_ToStatus REFERENCES ref.OrderStatuses (OrderStatusId),
    StatusChangeReasonId smallint          NULL CONSTRAINT FK_OrderStatusHistory_Reasons REFERENCES ref.StatusChangeReasons (StatusChangeReasonId),
    ChangedByUserId      nvarchar(64)      NULL CONSTRAINT FK_OrderStatusHistory_Users REFERENCES auth.Users (Id),
    ChangedAt            datetimeoffset(7) NOT NULL CONSTRAINT DF_OrderStatusHistory_ChangedAt DEFAULT SYSDATETIMEOFFSET(),
    Note                 nvarchar(500)     NULL,
    CONSTRAINT FK_OrderStatusHistory_Transitions FOREIGN KEY (FromStatusId, ToStatusId) REFERENCES ref.OrderStatusTransitions (FromStatusId, ToStatusId)
);
CREATE INDEX IX_OrderStatusHistory_OrderId_ChangedAt ON sales.OrderStatusHistory (OrderId, ChangedAt);

CREATE TABLE sales.OrderReturns
(
    Id             int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_OrderReturns PRIMARY KEY,
    ReturnNumber   varchar(32)       NOT NULL CONSTRAINT UQ_OrderReturns_ReturnNumber UNIQUE,
    OrderId        int               NOT NULL CONSTRAINT FK_OrderReturns_Orders REFERENCES sales.Orders (Id),
    ReturnReasonId smallint          NOT NULL CONSTRAINT FK_OrderReturns_Reasons REFERENCES ref.ReturnReasons (ReturnReasonId),
    ReturnStatusId tinyint           NOT NULL CONSTRAINT FK_OrderReturns_Statuses REFERENCES ref.ReturnStatuses (ReturnStatusId),
    RequestedAt    datetimeoffset(7) NOT NULL,
    ReceivedAt     datetimeoffset(7) NULL,
    ClosedAt       datetimeoffset(7) NULL,
    CustomerNote   nvarchar(500)     NULL,
    CONSTRAINT UQ_OrderReturns_Id_OrderId UNIQUE (Id, OrderId),
    CONSTRAINT CK_OrderReturns_Received CHECK (ReceivedAt IS NULL OR ReceivedAt >= RequestedAt),
    CONSTRAINT CK_OrderReturns_Closed CHECK (ClosedAt IS NULL OR ClosedAt >= RequestedAt),
    CONSTRAINT CK_OrderReturns_StatusDates CHECK (ReturnStatusId <> 1 OR (ReceivedAt IS NULL AND ClosedAt IS NULL))
);
CREATE INDEX IX_OrderReturns_OrderId ON sales.OrderReturns (OrderId);

-- OrderId is repeated here only so the two composite FKs can prove the returned item
-- belongs to the same order as the return.
CREATE TABLE sales.OrderReturnItems
(
    OrderReturnId int NOT NULL,
    OrderItemId   int NOT NULL,
    OrderId       int NOT NULL,
    Quantity      int NOT NULL,
    CONSTRAINT PK_OrderReturnItems PRIMARY KEY (OrderReturnId, OrderItemId),
    CONSTRAINT FK_OrderReturnItems_OrderReturns FOREIGN KEY (OrderReturnId, OrderId) REFERENCES sales.OrderReturns (Id, OrderId),
    CONSTRAINT FK_OrderReturnItems_OrderItems FOREIGN KEY (OrderItemId, OrderId) REFERENCES sales.OrderItems (Id, OrderId),
    CONSTRAINT CK_OrderReturnItems_Quantity CHECK (Quantity > 0)
);
CREATE INDEX IX_OrderReturnItems_OrderItemId ON sales.OrderReturnItems (OrderItemId);
GO

/* ---------------------------------------------------------------- payment: stored methods (tokenised only) */
-- No card numbers or security codes are stored. GatewayToken is the processor's reference
-- to the vaulted instrument; only display data (brand, last four, expiry) is kept here.
CREATE TABLE payment.PaymentMethods
(
    Id                  int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_PaymentMethods PRIMARY KEY,
    UserId              nvarchar(64)      NOT NULL CONSTRAINT FK_PaymentMethods_Users REFERENCES auth.Users (Id) ON DELETE CASCADE,
    PaymentMethodTypeId tinyint           NOT NULL CONSTRAINT FK_PaymentMethods_Types REFERENCES ref.PaymentMethodTypes (PaymentMethodTypeId),
    BillingAddressId    int               NULL,
    GatewayToken        varchar(64)       NOT NULL CONSTRAINT UQ_PaymentMethods_GatewayToken UNIQUE,
    IsDefault           bit               NOT NULL CONSTRAINT DF_PaymentMethods_IsDefault DEFAULT 0,
    CreatedAt           datetimeoffset(7) NOT NULL CONSTRAINT DF_PaymentMethods_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    RemovedAt           datetimeoffset(7) NULL, -- soft delete: attempts keep referencing removed methods
    CONSTRAINT UQ_PaymentMethods_Id_Type UNIQUE (Id, PaymentMethodTypeId),
    -- Billing address must belong to the same customer.
    CONSTRAINT FK_PaymentMethods_BillingAddress FOREIGN KEY (BillingAddressId, UserId) REFERENCES customer.Addresses (Id, UserId),
    CONSTRAINT CK_PaymentMethods_Removed CHECK (RemovedAt IS NULL OR RemovedAt >= CreatedAt),
    CONSTRAINT CK_PaymentMethods_DefaultActive CHECK (IsDefault = 0 OR RemovedAt IS NULL)
);
CREATE INDEX IX_PaymentMethods_UserId ON payment.PaymentMethods (UserId);
CREATE UNIQUE INDEX UQ_PaymentMethods_OneDefaultPerUser ON payment.PaymentMethods (UserId) WHERE IsDefault = 1;

CREATE TABLE payment.CardDetails
(
    PaymentMethodId     int           NOT NULL CONSTRAINT PK_CardDetails PRIMARY KEY,
    PaymentMethodTypeId tinyint       NOT NULL,
    CardBrandId         tinyint       NOT NULL CONSTRAINT FK_CardDetails_CardBrands REFERENCES ref.CardBrands (CardBrandId),
    Last4               char(4)       NOT NULL,
    ExpMonth            tinyint       NOT NULL,
    ExpYear             smallint      NOT NULL,
    CardholderName      nvarchar(100) NOT NULL,
    CONSTRAINT FK_CardDetails_PaymentMethods FOREIGN KEY (PaymentMethodId, PaymentMethodTypeId) REFERENCES payment.PaymentMethods (Id, PaymentMethodTypeId) ON DELETE CASCADE,
    CONSTRAINT CK_CardDetails_Type CHECK (PaymentMethodTypeId IN (1, 3, 4)),
    CONSTRAINT CK_CardDetails_Last4 CHECK (Last4 NOT LIKE '%[^0-9]%'),
    CONSTRAINT CK_CardDetails_ExpMonth CHECK (ExpMonth BETWEEN 1 AND 12),
    CONSTRAINT CK_CardDetails_ExpYear CHECK (ExpYear BETWEEN 2000 AND 2100)
);

CREATE TABLE payment.WalletDetails
(
    PaymentMethodId     int           NOT NULL CONSTRAINT PK_WalletDetails PRIMARY KEY,
    PaymentMethodTypeId tinyint       NOT NULL,
    AccountEmailMasked  nvarchar(256) NOT NULL, -- e.g. m***@example.com
    CONSTRAINT FK_WalletDetails_PaymentMethods FOREIGN KEY (PaymentMethodId, PaymentMethodTypeId) REFERENCES payment.PaymentMethods (Id, PaymentMethodTypeId) ON DELETE CASCADE,
    CONSTRAINT CK_WalletDetails_Type CHECK (PaymentMethodTypeId = 2),
    CONSTRAINT CK_WalletDetails_Masked CHECK (AccountEmailMasked LIKE '%*%@%')
);

CREATE TABLE payment.BankAccountDetails
(
    PaymentMethodId     int           NOT NULL CONSTRAINT PK_BankAccountDetails PRIMARY KEY,
    PaymentMethodTypeId tinyint       NOT NULL,
    BankName            nvarchar(100) NOT NULL,
    AccountType         varchar(10)   NOT NULL,
    AccountLast4        char(4)       NOT NULL,
    CONSTRAINT FK_BankAccountDetails_PaymentMethods FOREIGN KEY (PaymentMethodId, PaymentMethodTypeId) REFERENCES payment.PaymentMethods (Id, PaymentMethodTypeId) ON DELETE CASCADE,
    CONSTRAINT CK_BankAccountDetails_Type CHECK (PaymentMethodTypeId = 5),
    CONSTRAINT CK_BankAccountDetails_AccountType CHECK (AccountType IN ('CHECKING', 'SAVINGS')),
    CONSTRAINT CK_BankAccountDetails_Last4 CHECK (AccountLast4 NOT LIKE '%[^0-9]%')
);
GO

/* ---------------------------------------------------------------- payment: attempts and refunds */
CREATE TABLE payment.PaymentAttempts
(
    Id                     bigint IDENTITY(1, 1) NOT NULL CONSTRAINT PK_PaymentAttempts PRIMARY KEY,
    OrderId                int               NOT NULL CONSTRAINT FK_PaymentAttempts_Orders REFERENCES sales.Orders (Id),
    AttemptNumber          tinyint           NOT NULL,
    PaymentMethodId        int               NULL CONSTRAINT FK_PaymentAttempts_PaymentMethods REFERENCES payment.PaymentMethods (Id), -- NULL = guest / one-time
    Amount                 decimal(18, 2)    NOT NULL,
    CurrencyCode           char(3)           NOT NULL CONSTRAINT FK_PaymentAttempts_Currencies REFERENCES ref.Currencies (CurrencyCode),
    PaymentAttemptStatusId tinyint           NOT NULL CONSTRAINT FK_PaymentAttempts_Statuses REFERENCES ref.PaymentAttemptStatuses (PaymentAttemptStatusId),
    DeclineReasonId        smallint          NULL CONSTRAINT FK_PaymentAttempts_DeclineReasons REFERENCES ref.DeclineReasons (DeclineReasonId),
    GatewayReference       varchar(64)       NOT NULL CONSTRAINT UQ_PaymentAttempts_GatewayReference UNIQUE,
    AttemptedAt            datetimeoffset(7) NOT NULL CONSTRAINT DF_PaymentAttempts_AttemptedAt DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT UQ_PaymentAttempts_OrderId_AttemptNumber UNIQUE (OrderId, AttemptNumber),
    CONSTRAINT UQ_PaymentAttempts_Id_Status UNIQUE (Id, PaymentAttemptStatusId),
    CONSTRAINT CK_PaymentAttempts_AttemptNumber CHECK (AttemptNumber > 0),
    CONSTRAINT CK_PaymentAttempts_Amount CHECK (Amount > 0),
    -- Every failure is documented with a reason; successes and pending attempts have none.
    CONSTRAINT CK_PaymentAttempts_DeclineReason CHECK ((PaymentAttemptStatusId IN (2, 3) AND DeclineReasonId IS NOT NULL) OR (PaymentAttemptStatusId IN (1, 4) AND DeclineReasonId IS NULL))
);
CREATE INDEX IX_PaymentAttempts_PaymentMethodId ON payment.PaymentAttempts (PaymentMethodId);
CREATE INDEX IX_PaymentAttempts_Status_AttemptedAt ON payment.PaymentAttempts (PaymentAttemptStatusId, AttemptedAt);
CREATE UNIQUE INDEX UQ_PaymentAttempts_OneCapturePerOrder ON payment.PaymentAttempts (OrderId) WHERE PaymentAttemptStatusId = 1;

-- Refunds can only be issued against a captured attempt: CapturedStatusId is pinned to 1
-- and is part of the composite FK.
CREATE TABLE payment.Refunds
(
    Id               int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Refunds PRIMARY KEY,
    PaymentAttemptId bigint            NOT NULL,
    CapturedStatusId tinyint           NOT NULL CONSTRAINT DF_Refunds_CapturedStatusId DEFAULT 1,
    OrderReturnId    int               NULL CONSTRAINT FK_Refunds_OrderReturns REFERENCES sales.OrderReturns (Id),
    RefundReasonId   tinyint           NOT NULL CONSTRAINT FK_Refunds_Reasons REFERENCES ref.RefundReasons (RefundReasonId),
    RefundStatusId   tinyint           NOT NULL CONSTRAINT FK_Refunds_Statuses REFERENCES ref.RefundStatuses (RefundStatusId),
    Amount           decimal(18, 2)    NOT NULL,
    GatewayReference varchar(64)       NOT NULL CONSTRAINT UQ_Refunds_GatewayReference UNIQUE,
    RequestedAt      datetimeoffset(7) NOT NULL,
    CompletedAt      datetimeoffset(7) NULL,
    CONSTRAINT FK_Refunds_CapturedAttempt FOREIGN KEY (PaymentAttemptId, CapturedStatusId) REFERENCES payment.PaymentAttempts (Id, PaymentAttemptStatusId),
    CONSTRAINT CK_Refunds_CapturedOnly CHECK (CapturedStatusId = 1),
    CONSTRAINT CK_Refunds_Amount CHECK (Amount > 0),
    CONSTRAINT CK_Refunds_Completed CHECK (CompletedAt IS NULL OR CompletedAt >= RequestedAt),
    CONSTRAINT CK_Refunds_ReturnReason CHECK ((RefundReasonId = 2 AND OrderReturnId IS NOT NULL) OR (RefundReasonId <> 2 AND OrderReturnId IS NULL))
);
CREATE INDEX IX_Refunds_PaymentAttemptId ON payment.Refunds (PaymentAttemptId);
CREATE INDEX IX_Refunds_OrderReturnId ON payment.Refunds (OrderReturnId);
GO
COMMIT TRANSACTION;
GO
