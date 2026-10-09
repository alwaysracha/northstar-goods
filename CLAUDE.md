# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

"Northstar Goods": a single-deployable ASP.NET Core 10 MVC storefront (Razor views, Identity, EF Core SQL Server provider) backed by SQL Server 2025 (Developer edition, in Docker). The database is a hand-written T-SQL "legacy-style" schema in `database/`. One web project (`src/EcommerceApp.Web`), a unit test project and an integration test project, plus Puppeteer browser smoke scripts in `tests/browser`. Package versions are managed centrally in `Directory.Packages.props` (don't put versions in `.csproj` files).

## Commands

Everything is meant to run through Docker; no host .NET SDK is assumed. `scripts/compose.sh` is a drop-in for `docker compose` (it downloads a project-local Compose into `.tools/` if the plugin is missing).

```bash
./scripts/compose.sh up --build --wait -d        # sqlserver -> db-deploy (one-shot) -> web on http://localhost:5180 (APP_PORT)
./scripts/compose.sh run --rm db-deploy          # re-apply database/ scripts after editing them (idempotent)
./scripts/compose.sh down                        # add -v to wipe the mssql-data volume and verify a fresh deploy+seed
./scripts/compose.sh --profile test run --rm tests   # full test suite (unit + integration via Testcontainers)
./scripts/compose.sh --profile browser run --rm browser [node tests/browser/<commerce|admin>-smoke.js]
git add -A && ./scripts/security-scan.py         # scans STAGED additions for credentials / Process.Start / FromSqlRaw etc.
```

The `tests` image builds the solution at image-build time, so source changes require `--build` (`./scripts/compose.sh --profile test build tests`). The image's entrypoint is `dotnet test EcommerceApp.sln --no-build -c Release`, so extra args filter tests, e.g. a single test:

```bash
./scripts/compose.sh --profile test run --rm --build tests --filter "FullyQualifiedName~DiscountServiceTests"
```

Ad-hoc SDK commands (vulnerable-package audit) run in `mcr.microsoft.com/dotnet/sdk:10.0` with the repo mounted; see README. **Pass `--user "$(id -u):$(id -g)"` (and `-e HOME=/tmp`) when mounting the repo**, otherwise `bin/`, `obj/` and `.tools/` end up root-owned on the host.

Browser smoke scripts and commerce/admin scripts create QA records; run against a disposable dev DB. Screenshots go to `.hermes/artifacts`.

## Architecture

- **`Program.cs`** wires everything: Identity (cookie paths `/account/login`, `/account/access-denied`), scoped services, security headers, routes (`admin/{controller}/{action}` area, `products/{slug}` → `Catalog/Detail`, default). The app never creates or migrates the schema. Outside Development a connection string is mandatory. `public partial class Program` exists for `WebApplicationFactory`.
- **Database (`database/`)**: the T-SQL is the schema source of truth, and there are **no EF migrations**. `deploy.sh` (sqlcmd, run by the `db-deploy` compose service) runs `000_create_database.sql` (always), then `migrations/NNNN_*.sql` (once each, journaled in `dbo.SchemaVersions`), then `programmability/` views and procs (always, `CREATE OR ALTER`), then `security/app_login.sql` (always), then `seed/NNNN_*.sql` (once, if `SEED_DEMO_DATA=true`). Schema changes = a **new** numbered migration (never edit an applied one) + matching EF mapping. Schemas: `auth` (Identity), `catalog`, `customer`, `sales`, `payment`, `ref` (lookup tables whose ids equal the C# enum values), `reporting` (views). Business rules live in CHECK constraints and composite FKs (e.g. `Orders.Total = Subtotal - DiscountTotal + ShippingTotal`, declines must have a reason, history rows must follow `ref.OrderStatusTransitions`). Temp tables in scripts need `COLLATE DATABASE_DEFAULT` on string columns (tempdb collation differs). The app connects as the least-privilege `northstar_app` login (DML on data schemas, SELECT on `ref`/`reporting`, EXECUTE on `sales`; no DDL).
- **Seed**: `seed/0001` recreates the catalog (8 categories, 50 products, same SKUs/prices/stock as before), discount codes (`WELCOME10`/`TAKE20` + historical ones), roles and the demo accounts (admin `admin@northstar.local`, maya/leo/sam with orders `NST-2026-0001..0003`). `seed/0002` deterministically generates 1,000 customers (`first.last.NNNN@example.com`), addresses, tokenised payment methods and ~5,200 orders with failed/recovered/cancelled/returned outcomes, using a fixed "now" of 2026-10-08. Every seeded account's password is `LocalDemo!2026` (constants in `Data/DemoAccounts.cs`).
- **Domain**: all entities live in `Domain/Entities/Entities.cs`; table/column mapping (schemas, enum → `tinyint` `*Id` columns, precision, concurrency tokens) is in `Data/ApplicationDbContext.cs`. `Order` stores the shipping address as `Ship*` columns; `ShippingAddress` is a computed, unmapped string. `ModelConfigurationTests` (unit) and `DatabaseSchemaTests` (integration, including a check that every EF-mapped column exists) assert on this.
- **Services** (`Services/*`, registered as scoped interfaces) hold the business logic; controllers stay thin.
  - Cart: `CartIdentity` issues an anonymous `northstar.cart` cookie key; `CartService.MergeAsync` folds the guest cart into the user's cart on login/register (`AccountController`).
  - Checkout: `CheckoutService.PlaceAsync` runs in a **Serializable transaction**, decrements stock with conditional `ExecuteUpdateAsync` (also bumps `Product.StockVersion`, a concurrency token), snapshots item names/prices and discount code onto the order, records a `DiscountRedemption`, and deletes the cart. Idempotency comes from a hashed `CheckoutToken` (cart id + form token); on DB failure it rolls back and returns any existing order for that token. Payment is simulated only — no card fields by design.
  - Discounts: `DiscountService.EvaluateAsync` validates min-order/usage/caps; used by both cart display and checkout.
  - Orders: `OrderStatusPolicy` is the app-side source of allowed status transitions (Pending→Processing/Cancelled, Processing→Shipped/Cancelled, Shipped→Delivered), mirrored by `ref.OrderStatusTransitions`. Admin status changes check the policy, then call `sales.usp_ChangeOrderStatus`, which re-validates, applies optimistic concurrency and writes `sales.OrderStatusHistory` atomically. Checkout writes the initial history rows and a captured `PaymentAttempt` in its transaction, and rejects countries missing from `ref.Countries`.
- **Admin area** (`Areas/Admin`): all controllers derive from `AdminControllerBase` (`[Authorize(Roles = "Administrator")]`) and use `_AdminLayout`. Customer order history (`OrdersController`) is owner-only.
- POST actions use `[ValidateAntiForgeryToken]`; keep that convention.

## Tests

- Unit tests (xUnit v3) use EF InMemory and don't need Docker.
- Integration tests share one `IntegrationTestFactory` collection fixture: a Testcontainers `mssql/server:2025-latest`, deployed by `SqlScriptDeployer` from the same `database/` scripts (copied to the test output via the csproj), with the app connecting as `northstar_app`. Use `factory.AdminConnectionString` (sa) for raw assertions. The connection string is passed with `UseSetting`, because `ConfigureAppConfiguration` arrives after `Program.cs` reads it. Tag new integration test classes with `[Collection(IntegrationTestCollection.Name)]`. Tests share one database, so test-inserted rows must satisfy the cross-table checks in `SeedDataTests`. When run via Compose, tests reach Docker through the mounted socket (`TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`).

## Conventions

- `.editorconfig`: LF, 4-space indent (2 for json/yml/csproj/props). Code style is compact: primary constructors, sealed classes, records for results, multiple statements per line in short methods.
- Secrets go in the git-ignored `.env` (see `.env.example`); committed credentials are development-only and the security scan allow-lists only `DemoPassword`.
- `.hermes/plans/` contains the original milestone implementation plan for scope reference.
