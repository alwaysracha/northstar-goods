/*
    0001_schemas_and_reference_data.sql
    Schemas and lookup (reference) tables. Every coded value used elsewhere in the
    database is a row here and is referenced by a foreign key, never a free-text column.
    Ids of the status tables match the application's C# enum values.
*/
SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;
GO
CREATE SCHEMA ref AUTHORIZATION dbo;      -- lookup / code tables
GO
CREATE SCHEMA auth AUTHORIZATION dbo;     -- ASP.NET Core Identity
GO
CREATE SCHEMA catalog AUTHORIZATION dbo;  -- categories and products
GO
CREATE SCHEMA customer AUTHORIZATION dbo; -- customer-owned data (addresses)
GO
CREATE SCHEMA sales AUTHORIZATION dbo;    -- carts, discounts, orders, returns
GO
CREATE SCHEMA payment AUTHORIZATION dbo;  -- stored payment methods, attempts, refunds
GO
CREATE SCHEMA reporting AUTHORIZATION dbo; -- read-only views
GO

CREATE TABLE ref.Countries
(
    CountryCode char(2)      NOT NULL CONSTRAINT PK_Countries PRIMARY KEY,
    Name        nvarchar(80) NOT NULL CONSTRAINT UQ_Countries_Name UNIQUE,
    CONSTRAINT CK_Countries_Code CHECK (CountryCode COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z]')
);

CREATE TABLE ref.Currencies
(
    CurrencyCode char(3)      NOT NULL CONSTRAINT PK_Currencies PRIMARY KEY,
    Name         nvarchar(60) NOT NULL,
    MinorUnits   tinyint      NOT NULL CONSTRAINT CK_Currencies_MinorUnits CHECK (MinorUnits <= 4),
    CONSTRAINT CK_Currencies_Code CHECK (CurrencyCode COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]')
);

-- C# enum OrderStatus
CREATE TABLE ref.OrderStatuses
(
    OrderStatusId tinyint      NOT NULL CONSTRAINT PK_OrderStatuses PRIMARY KEY,
    Code          varchar(30)  NOT NULL CONSTRAINT UQ_OrderStatuses_Code UNIQUE,
    Name          nvarchar(50) NOT NULL,
    IsFinal       bit          NOT NULL
);

-- The only allowed order status changes (mirrors OrderStatusPolicy in the app).
CREATE TABLE ref.OrderStatusTransitions
(
    FromStatusId tinyint NOT NULL CONSTRAINT FK_OrderStatusTransitions_From REFERENCES ref.OrderStatuses (OrderStatusId),
    ToStatusId   tinyint NOT NULL CONSTRAINT FK_OrderStatusTransitions_To   REFERENCES ref.OrderStatuses (OrderStatusId),
    CONSTRAINT PK_OrderStatusTransitions PRIMARY KEY (FromStatusId, ToStatusId),
    CONSTRAINT CK_OrderStatusTransitions_Distinct CHECK (FromStatusId <> ToStatusId)
);

-- C# enum PaymentStatus (order-level payment state)
CREATE TABLE ref.PaymentStatuses
(
    PaymentStatusId tinyint      NOT NULL CONSTRAINT PK_PaymentStatuses PRIMARY KEY,
    Code            varchar(30)  NOT NULL CONSTRAINT UQ_PaymentStatuses_Code UNIQUE,
    Name            nvarchar(50) NOT NULL
);

-- C# enum DiscountKind
CREATE TABLE ref.DiscountKinds
(
    DiscountKindId tinyint      NOT NULL CONSTRAINT PK_DiscountKinds PRIMARY KEY,
    Code           varchar(30)  NOT NULL CONSTRAINT UQ_DiscountKinds_Code UNIQUE,
    Name           nvarchar(50) NOT NULL
);

-- Why an order changed status (cancellation reasons, fulfilment events, ...).
CREATE TABLE ref.StatusChangeReasons
(
    StatusChangeReasonId smallint      NOT NULL CONSTRAINT PK_StatusChangeReasons PRIMARY KEY,
    Code                 varchar(40)   NOT NULL CONSTRAINT UQ_StatusChangeReasons_Code UNIQUE,
    Description          nvarchar(200) NOT NULL
);

-- Stored payment method types and which detail table describes them.
CREATE TABLE ref.PaymentMethodTypes
(
    PaymentMethodTypeId tinyint      NOT NULL CONSTRAINT PK_PaymentMethodTypes PRIMARY KEY,
    Code                varchar(30)  NOT NULL CONSTRAINT UQ_PaymentMethodTypes_Code UNIQUE,
    Name                nvarchar(50) NOT NULL,
    DetailKind          varchar(10)  NOT NULL CONSTRAINT CK_PaymentMethodTypes_DetailKind CHECK (DetailKind IN ('CARD', 'WALLET', 'BANK'))
);

CREATE TABLE ref.CardBrands
(
    CardBrandId tinyint      NOT NULL CONSTRAINT PK_CardBrands PRIMARY KEY,
    Code        varchar(20)  NOT NULL CONSTRAINT UQ_CardBrands_Code UNIQUE,
    Name        nvarchar(40) NOT NULL
);

CREATE TABLE ref.PaymentAttemptStatuses
(
    PaymentAttemptStatusId tinyint      NOT NULL CONSTRAINT PK_PaymentAttemptStatuses PRIMARY KEY,
    Code                   varchar(20)  NOT NULL CONSTRAINT UQ_PaymentAttemptStatuses_Code UNIQUE,
    Name                   nvarchar(40) NOT NULL,
    IsFailure              bit          NOT NULL
);

-- Gateway decline / error reasons, with the ISO 8583-style processor response code.
CREATE TABLE ref.DeclineReasons
(
    DeclineReasonId smallint      NOT NULL CONSTRAINT PK_DeclineReasons PRIMARY KEY,
    Code            varchar(40)   NOT NULL CONSTRAINT UQ_DeclineReasons_Code UNIQUE,
    ProcessorCode   varchar(10)   NOT NULL,
    Description     nvarchar(200) NOT NULL,
    IsRetryable     bit           NOT NULL
);

CREATE TABLE ref.ReturnReasons
(
    ReturnReasonId smallint      NOT NULL CONSTRAINT PK_ReturnReasons PRIMARY KEY,
    Code           varchar(40)   NOT NULL CONSTRAINT UQ_ReturnReasons_Code UNIQUE,
    Description    nvarchar(200) NOT NULL
);

CREATE TABLE ref.ReturnStatuses
(
    ReturnStatusId tinyint      NOT NULL CONSTRAINT PK_ReturnStatuses PRIMARY KEY,
    Code           varchar(20)  NOT NULL CONSTRAINT UQ_ReturnStatuses_Code UNIQUE,
    Name           nvarchar(40) NOT NULL
);

CREATE TABLE ref.RefundReasons
(
    RefundReasonId tinyint       NOT NULL CONSTRAINT PK_RefundReasons PRIMARY KEY,
    Code           varchar(30)   NOT NULL CONSTRAINT UQ_RefundReasons_Code UNIQUE,
    Description    nvarchar(200) NOT NULL
);

CREATE TABLE ref.RefundStatuses
(
    RefundStatusId tinyint      NOT NULL CONSTRAINT PK_RefundStatuses PRIMARY KEY,
    Code           varchar(20)  NOT NULL CONSTRAINT UQ_RefundStatuses_Code UNIQUE,
    Name           nvarchar(40) NOT NULL
);
GO

INSERT ref.Countries (CountryCode, Name) VALUES
    ('US', N'United States'), ('CA', N'Canada'), ('GB', N'United Kingdom'), ('AU', N'Australia'), ('IN', N'India'),
    ('NZ', N'New Zealand'), ('IE', N'Ireland'), ('DE', N'Germany'), ('FR', N'France'), ('NL', N'Netherlands'),
    ('ES', N'Spain'), ('IT', N'Italy'), ('SE', N'Sweden'), ('JP', N'Japan'), ('SG', N'Singapore'),
    ('MX', N'Mexico'), ('BR', N'Brazil'), ('ZA', N'South Africa'), ('AE', N'United Arab Emirates'), ('CH', N'Switzerland');

INSERT ref.Currencies (CurrencyCode, Name, MinorUnits) VALUES
    ('USD', N'US Dollar', 2), ('CAD', N'Canadian Dollar', 2), ('GBP', N'Pound Sterling', 2), ('AUD', N'Australian Dollar', 2), ('INR', N'Indian Rupee', 2);

INSERT ref.OrderStatuses (OrderStatusId, Code, Name, IsFinal) VALUES
    (0, 'PENDING', N'Pending', 0), (1, 'PROCESSING', N'Processing', 0), (2, 'SHIPPED', N'Shipped', 0),
    (3, 'DELIVERED', N'Delivered', 1), (4, 'CANCELLED', N'Cancelled', 1);

INSERT ref.OrderStatusTransitions (FromStatusId, ToStatusId) VALUES
    (0, 1), (0, 4), (1, 2), (1, 4), (2, 3);

INSERT ref.PaymentStatuses (PaymentStatusId, Code, Name) VALUES
    (0, 'PENDING', N'Pending'), (1, 'PAID', N'Paid'), (2, 'FAILED', N'Failed'),
    (3, 'REFUNDED', N'Refunded'), (4, 'PARTIALLY_REFUNDED', N'Partially refunded');

INSERT ref.DiscountKinds (DiscountKindId, Code, Name) VALUES
    (0, 'PERCENTAGE', N'Percentage'), (1, 'FIXED_AMOUNT', N'Fixed amount');

INSERT ref.StatusChangeReasons (StatusChangeReasonId, Code, Description) VALUES
    (1,  'ORDER_PLACED',          N'Customer submitted the order.'),
    (2,  'PAYMENT_CAPTURED',      N'Payment was captured; order released for fulfilment.'),
    (3,  'PAYMENT_FAILED',        N'All payment attempts were declined or errored.'),
    (4,  'CUSTOMER_REQUEST',      N'Customer asked to cancel before shipment.'),
    (5,  'OUT_OF_STOCK',          N'An item could not be fulfilled from stock.'),
    (6,  'FRAUD_REVIEW_REJECTED', N'Order failed manual fraud review.'),
    (7,  'ADDRESS_UNDELIVERABLE', N'Carrier could not validate the shipping address.'),
    (8,  'HANDED_TO_CARRIER',     N'Parcel collected by the carrier.'),
    (9,  'CARRIER_DELIVERED',     N'Carrier confirmed delivery.'),
    (10, 'ADMIN_UPDATE',          N'Status changed manually by an administrator.');

INSERT ref.PaymentMethodTypes (PaymentMethodTypeId, Code, Name, DetailKind) VALUES
    (1, 'CARD', N'Credit or debit card', 'CARD'), (2, 'PAYPAL', N'PayPal', 'WALLET'),
    (3, 'APPLE_PAY', N'Apple Pay', 'CARD'), (4, 'GOOGLE_PAY', N'Google Pay', 'CARD'),
    (5, 'BANK_ACCOUNT', N'Bank account (ACH)', 'BANK');

INSERT ref.CardBrands (CardBrandId, Code, Name) VALUES
    (1, 'VISA', N'Visa'), (2, 'MASTERCARD', N'Mastercard'), (3, 'AMEX', N'American Express'), (4, 'DISCOVER', N'Discover');

INSERT ref.PaymentAttemptStatuses (PaymentAttemptStatusId, Code, Name, IsFailure) VALUES
    (1, 'CAPTURED', N'Captured', 0), (2, 'DECLINED', N'Declined', 1), (3, 'ERROR', N'Gateway error', 1), (4, 'PENDING', N'Pending', 0);

INSERT ref.DeclineReasons (DeclineReasonId, Code, ProcessorCode, Description, IsRetryable) VALUES
    (1, 'INSUFFICIENT_FUNDS',    '51', N'The account does not have enough funds.', 1),
    (2, 'CARD_EXPIRED',          '54', N'The card has expired.', 0),
    (3, 'DO_NOT_HONOR',          '05', N'The issuer declined without giving a reason.', 1),
    (4, 'SUSPECTED_FRAUD',       '59', N'The issuer suspects fraud.', 0),
    (5, 'AUTHENTICATION_FAILED', 'R1', N'3-D Secure authentication failed or was abandoned.', 1),
    (6, 'LIMIT_EXCEEDED',        '61', N'The transaction exceeds the card''s limit.', 1),
    (7, 'INVALID_ACCOUNT',       '14', N'The account number is invalid or closed.', 0),
    (8, 'PROCESSOR_TIMEOUT',     '91', N'The issuer or processor did not respond in time.', 1);

INSERT ref.ReturnReasons (ReturnReasonId, Code, Description) VALUES
    (1, 'DAMAGED',          N'Item arrived damaged.'),
    (2, 'WRONG_ITEM',       N'A different item was delivered.'),
    (3, 'NOT_AS_DESCRIBED', N'Item does not match its description.'),
    (4, 'NO_LONGER_NEEDED', N'Customer changed their mind.'),
    (5, 'ARRIVED_LATE',     N'Item arrived after it was needed.'),
    (6, 'QUALITY',          N'Customer was unhappy with the quality.');

INSERT ref.ReturnStatuses (ReturnStatusId, Code, Name) VALUES
    (1, 'REQUESTED', N'Requested'), (2, 'RECEIVED', N'Received'), (3, 'REFUNDED', N'Refunded'), (4, 'REJECTED', N'Rejected');

INSERT ref.RefundReasons (RefundReasonId, Code, Description) VALUES
    (1, 'CANCELLATION', N'Order cancelled after payment was captured.'),
    (2, 'RETURN',       N'Items returned by the customer.'),
    (3, 'GOODWILL',     N'Discretionary refund issued by customer service.');

INSERT ref.RefundStatuses (RefundStatusId, Code, Name) VALUES
    (1, 'PENDING', N'Pending'), (2, 'SUCCEEDED', N'Succeeded'), (3, 'FAILED', N'Failed');
GO
COMMIT TRANSACTION;
GO
