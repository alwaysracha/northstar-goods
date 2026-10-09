# Northstar Goods

A single-deployable ASP.NET Core 10 MVC storefront backed by a SQL Server 2025 database. Features: a responsive catalog, branded Identity, persistent guest and account carts, discounts, transactional simulated checkout, owner-only customer order history, and a role-protected administration area.

The database is a hand-written T-SQL schema in [`database/`](database/), with 1,000 sample customers and about 5,200 orders. It runs in Docker on the local machine.

## Prerequisites

- Docker with Compose. `scripts/compose.sh` uses the Compose plugin when available; otherwise it downloads a project-local copy into the ignored `.tools` directory.
- About 3 GB of free memory. SQL Server is capped at 2 GB by default (`MSSQL_MEMORY_LIMIT_MB`).
- Free local ports `5180` (app) and `1433` (SQL Server). Both can be changed in `.env`.
- `curl` for the health check. No host .NET SDK or SQL Server installation is needed.

All commands run from the repository root.

## Quick start

```bash
cp .env.example .env                 # optional: choose your own local passwords and ports
./scripts/compose.sh up --build --wait -d
curl --fail http://localhost:5180/health
```

`up` starts three services in order:

1. `sqlserver`: SQL Server 2025 Developer edition. Restarts automatically; data is kept in the `mssql-data` volume.
2. `db-deploy`: a one-shot job that runs `database/deploy.sh`, which creates the database, applies the schema, views, procedures and security, and loads the sample data. It then exits.
3. `web`: the storefront, which starts only after the deploy succeeds.

The first start takes about a minute while SQL Server initialises. Open <http://localhost:5180>.

Stop everything with `./scripts/compose.sh down`. The database is kept. Add `-v` only when you want to delete it and start fresh.

## Sign in

Every seeded account uses the password `LocalDemo!2026`. These credentials are development-only and must never be reused elsewhere.

| Role | Email | Notes |
|---|---|---|
| Administrator | `admin@northstar.local` | Admin dashboard at <http://localhost:5180/admin> |
| Customer | `maya@example.local` | One delivered order, `NST-2026-0002` |
| Customer | `leo@example.local` | One shipped order, `NST-2026-0001` |
| Customer | `sam@example.local` | One cancelled and refunded order, `NST-2026-0003` |
| Customer | `isabella.miller.0979@example.com` | One of the 1,000 generated customers (11 orders) |

Generated customers follow the pattern `firstname.lastname.NNNN@example.com`. To list some with their order counts:

```sql
SELECT TOP 20 Email, OrdersPlaced FROM reporting.vw_CustomerLifetimeValue ORDER BY OrdersPlaced DESC;
```

Customers see their purchases at <http://localhost:5180/orders>. Administrators manage categories, products, inventory, discounts and order status.

Discount codes: `WELCOME10` (10% off orders of at least $50, capped at $40) and `TAKE20` ($20 off orders of at least $150). `VIP30` has already used all 50 of its redemptions in the sample data, so it shows the usage-limit message. Checkout is a simulation: the form intentionally has no card-number, security-code or real payment fields.

## Sample data

The seed is deterministic, so every fresh deploy produces identical rows. The history runs from June 2024 to 8 October 2026.

| | Count |
|---|---|
| Customers | 1,000 generated + 3 demo customers + 1 admin |
| Addresses | ~1,500 (1–3 per customer) |
| Stored payment methods | ~1,660: cards, Apple Pay, Google Pay, PayPal and bank accounts (tokenised; no card numbers) |
| Products / categories | 50 / 8 |
| Orders | 5,203 |

Order outcomes:

- About 80% were paid and delivered; recent ones are still processing or shipped.
- About 300 were declined once, then paid on a second attempt.
- 364 (7%) failed: 1–3 declined or errored payment attempts, each with a recorded reason (insufficient funds, expired card, suspected fraud, 3-D Secure failure, processor timeout and so on), and an order-history note.
- About 240 were cancelled after payment (customer request, out of stock, undeliverable address, fraud review) and refunded.
- 394 orders have returns, full or partial, most of them refunded.
- A handful of orders from the last 72 hours are still awaiting payment confirmation.

The app shows order and payment status. Decline reasons, payment methods, returns and refunds are in the database, readable through the reporting views below; they have no screens yet.

## Database

The schema is hand-written T-SQL in `database/` and is the source of truth. There are no EF migrations; EF Core maps onto the existing tables.

### Connect

Use any SQL Server client (Azure Data Studio, SSMS, DBeaver, `sqlcmd`):

| | |
|---|---|
| Server | `localhost,1433` (trust the server certificate) |
| Database | `NorthstarGoods` |
| Admin login | `sa` / `MSSQL_SA_PASSWORD` (default `LocalDev!Sa2026`) |
| Application login | `northstar_app` / `MSSQL_APP_PASSWORD` (default `LocalDev!App2026`) |

Or from the container:

```bash
./scripts/compose.sh exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -d NorthstarGoods
```

The port is published on `127.0.0.1` only. To reach the server from other machines, change the `ports` mapping in `docker-compose.yml`.

### Layout

| Path | Runs | Contents |
|---|---|---|
| `000_create_database.sql` | every deploy | Creates `NorthstarGoods`, enables read-committed snapshot, creates the `dbo.SchemaVersions` journal |
| `migrations/NNNN_*.sql` | once each | Schemas, reference tables, core tables and constraints |
| `programmability/` | every deploy | Reporting views and stored procedures (`CREATE OR ALTER`) |
| `security/app_login.sql` | every deploy | `northstar_app` login (the app's least-privilege account) and the `app_role` / `reporting_reader` roles |
| `seed/NNNN_*.sql` | once each, when `SEED_DEMO_DATA=true` | Catalog, demo accounts, generated customers and order history |

`database/deploy.sh` applies them in that order and records migrations and seeds in `dbo.SchemaVersions`, so re-running it is safe.

Schemas: `auth` (Identity), `catalog`, `customer`, `sales`, `payment`, `ref` (lookup codes) and `reporting` (views).

- Normalised to 3NF. Order lines, shipping addresses and totals are snapshots at purchase time by design: an order is a record of what was agreed.
- `CHECK` constraints enforce money totals (`Total = Subtotal - DiscountTotal + ShippingTotal`), status/payment consistency (nothing ships unpaid) and that every declined payment has a reason.
- Composite foreign keys enforce payment-method subtypes, refunds only against captured payments, returned items belonging to their order, and order-status changes only along allowed transitions.
- Every script and multi-step operation runs in a single transaction (`XACT_ABORT ON`).
- Payment methods are tokenised: brand, last 4 digits, expiry and a gateway token only.
- The application connects as `northstar_app`, which can read and write data but cannot change the schema.

### Views and procedures

| Object | Use |
|---|---|
| `reporting.vw_OrderSummary` | One row per order with status names, item counts, captured, refunded and net amounts |
| `reporting.vw_FailedPayments` | Every declined or errored payment attempt, its reason, and whether a later attempt succeeded |
| `reporting.vw_OrderTimeline` | Each order's status history with reasons and who made the change |
| `reporting.vw_ReturnsAndRefunds` | Returns with returned value and refund status |
| `reporting.vw_CustomerLifetimeValue` | Orders, spend, refunds and average order value per customer |
| `reporting.vw_ProductSalesPerformance` | Units sold, revenue and return rate per product |
| `reporting.vw_DailySales` | Orders, failures, cancellations and revenue per day |
| `reporting.vw_CustomerPaymentMethods` | Stored payment methods as masked display text |
| `sales.usp_ChangeOrderStatus` | Changes an order's status and writes its audit row in one transaction. The admin order page uses it |

Example queries:

```sql
-- Why payments fail
SELECT DeclineCode, COUNT(*) AS Attempts, SUM(CAST(RecoveredByLaterAttempt AS int)) AS RecoveredLater
FROM reporting.vw_FailedPayments GROUP BY DeclineCode ORDER BY Attempts DESC;

-- One order's full story
SELECT * FROM reporting.vw_OrderTimeline WHERE OrderNumber = 'NST-2026-0003' ORDER BY ChangedAt;

-- Last 14 days of sales
SELECT TOP 14 * FROM reporting.vw_DailySales ORDER BY SalesDate DESC;
```

### Change the schema

1. Add a new numbered file such as `database/migrations/0003_add_wishlists.sql`. Never edit a migration that has already been applied: it won't run again, and other environments would drift.
2. For views or procedures, edit `database/programmability/*.sql`; they are re-applied on every deploy.
3. Update the EF Core mapping in `src/EcommerceApp.Web/Data/ApplicationDbContext.cs` if the app uses the new columns. The `DatabaseSchemaTests` integration test fails if a mapped column is missing from the database.
4. Apply it with `./scripts/compose.sh run --rm db-deploy`, then run the tests.

### Reset the database

```bash
./scripts/compose.sh down -v
./scripts/compose.sh up --build --wait -d
```

To start with an empty database (schema, reference data and the app login, but no sample data), set `SEED_DEMO_DATA=false` in `.env` before `up`.

## Configuration

Settings go in the ignored `.env` file; copy `.env.example` to start. Committed values are development-only defaults.

| Variable | Default | Purpose |
|---|---|---|
| `MSSQL_SA_PASSWORD` | `LocalDev!Sa2026` | SQL Server `sa` password, used by the deploy job and for administration |
| `MSSQL_APP_PASSWORD` | `LocalDev!App2026` | Password for the app's `northstar_app` login; reset on every deploy |
| `MSSQL_DATABASE` | `NorthstarGoods` | Database name |
| `MSSQL_PORT` | `1433` | Local port for SQL Server |
| `MSSQL_MEMORY_LIMIT_MB` | `2048` | SQL Server memory cap |
| `SEED_DEMO_DATA` | `true` | Load sample data on first deploy |
| `APP_PORT` | `5180` | Local port for the storefront |

SQL Server passwords need 8+ characters from at least three of: uppercase, lowercase, digits, symbols. `MSSQL_SA_PASSWORD` only takes effect when the `mssql-data` volume is first created; to change it later, change it inside SQL Server or reset the database.

## Test

```bash
./scripts/compose.sh --profile test run --rm --build tests
```

Unit tests use EF Core InMemory. The integration suite starts a disposable SQL Server 2025 container with Testcontainers, deploys the same `database/` scripts (including the seed), runs the app against it as the least-privilege `northstar_app` login, and removes the container afterwards. It also checks the constraints, views and procedures, that the data satisfies cross-table business rules, and that the app login can't change the schema. The test container uses the host Docker socket; it never connects to or changes the application database.

Run a single test class:

```bash
./scripts/compose.sh --profile test run --rm --build tests --filter "FullyQualifiedName~SeedDataTests"
```

Run reproducible Chromium acceptance tests (screenshots are written under `.hermes/artifacts`):

```bash
./scripts/compose.sh --profile browser run --rm browser
./scripts/compose.sh --profile browser run --rm browser node tests/browser/commerce-smoke.js
./scripts/compose.sh --profile browser run --rm browser node tests/browser/admin-smoke.js
```

The commerce and admin browser scripts create QA records. Run them against a disposable or intentionally reset development database, not production.

Run the staged security scan and NuGet vulnerability audit:

```bash
git add -A && ./scripts/security-scan.py
docker run --rm -v "$PWD:/source" -w /source mcr.microsoft.com/dotnet/sdk:10.0 dotnet list EcommerceApp.sln package --vulnerable --include-transitive
```

## Troubleshooting

| Symptom | Fix |
|---|---|
| `up` fails with "port is already allocated" | Another service uses 1433 or 5180. Set `MSSQL_PORT` or `APP_PORT` in `.env`. |
| `sqlserver` exits right after starting | Usually a password that fails SQL Server's complexity rules, or too little memory. Check `./scripts/compose.sh logs sqlserver`. |
| `db-deploy` fails | `./scripts/compose.sh logs db-deploy` shows the failing script and SQL error. Fix the script, then run `./scripts/compose.sh run --rm db-deploy`. |
| App shows a database error after you changed `MSSQL_APP_PASSWORD` | Run `./scripts/compose.sh run --rm db-deploy` to reset the login's password, then `./scripts/compose.sh restart web`. |

Production hosting, secrets management, real payments and operational hardening are outside the current milestone.
