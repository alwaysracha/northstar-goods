# Northstar Goods

A single-deployable ASP.NET Core 10 MVC storefront backed by PostgreSQL. Milestones 0–3 include a responsive catalog, branded Identity, persistent guest/account carts, discounts, transactional simulated checkout, owner-only customer order history, and a role-protected administration area.

## Prerequisites

Docker and `curl`. No host .NET SDK or package installation is needed. All commands run from this repository root. `scripts/compose.sh` uses the Docker Compose plugin when available; otherwise it downloads a project-local copy into the ignored `.tools` directory.

## Run

```bash
cp .env.example .env                 # optional; choose a local-only password
./scripts/compose.sh up --build --wait -d
./scripts/compose.sh ps
curl --fail http://localhost:5180/health
```

Open <http://localhost:5180>. The application applies the checked-in migration and idempotent development seed at startup.

Signed-in customers can review their own purchases at <http://localhost:5180/orders>. Administrators can open the responsive dashboard at <http://localhost:5180/admin> to manage categories, products and inventory, approved local product imagery, discounts, and controlled order-status changes.

Use `WELCOME10` (10% off orders of at least $50, capped at $40) or `TAKE20` ($20 off orders of at least $150). Checkout is a simulation: the form intentionally has no card-number, security-code, or real payment fields.

## Test

The integration suite starts a disposable `postgres:17-alpine` database with Testcontainers, applies every migration, seeds it, and removes it after the suite. The Compose test container uses the host Docker socket; it never connects to or changes the application database.

```bash
./scripts/compose.sh --profile test run --rm tests
```

Run reproducible Chromium acceptance from the repository root (screenshots are written under `.hermes/artifacts`):

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

To verify EF migration drift inside the SDK container:

```bash
docker run --rm -v "$PWD:/source" -w /source mcr.microsoft.com/dotnet/sdk:10.0 sh -lc 'dotnet tool install dotnet-ef --version 10.0.0 --tool-path /tmp/tools && dotnet restore EcommerceApp.sln && /tmp/tools/dotnet-ef migrations has-pending-model-changes --project src/EcommerceApp.Web --startup-project src/EcommerceApp.Web'
```

To verify a clean migration and seed:

```bash
./scripts/compose.sh down -v
./scripts/compose.sh up --build --wait -d
./scripts/compose.sh exec postgres psql -U ecommerce -d ecommerce -c 'select count(*) from "Products";'
```

Expected product count: `50`; expected category count: `8`.

## Development-only accounts

These deterministic credentials are local demo data and must never be reused outside Development:

| Role | Email | Password |
|---|---|---|
| Administrator | `admin@northstar.local` | `LocalDemo!2026` |
| Customer | `maya@example.local` | `LocalDemo!2026` |
| Customer | `leo@example.local` | `LocalDemo!2026` |
| Customer | `sam@example.local` | `LocalDemo!2026` |

Stop the app with `./scripts/compose.sh down`. Add `-v` only when you intentionally want to delete the local database volume and verify a fresh start.

Secrets belong in the ignored `.env`; committed values are development-only defaults. Production hosting, secrets, payments, and operational hardening are outside the current milestone.
