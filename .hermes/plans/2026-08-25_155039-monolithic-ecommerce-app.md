# Monolithic E-commerce App Implementation Plan

> **For Hermes:** Execute this plan milestone-by-milestone with test-driven development, specification review, quality/security review, and real browser verification. Do not present scaffolding as an MVP.

**Goal:** Build a polished, responsive, locally runnable e-commerce application containing realistic catalog data, customer and guest purchasing flows, account/order history, discount codes, and an administration area.

**Architecture:** One deployable ASP.NET Core 10 MVC application will contain UI, business rules, authentication, administration, and persistence. PostgreSQL will run beside it through Docker Compose, while Entity Framework Core and ASP.NET Core Identity provide data access and account management. The internal code will be separated by responsibility without introducing microservices or unnecessary distributed infrastructure.

**Tech Stack:** .NET 10, ASP.NET Core MVC/Razor, ASP.NET Core Identity, Entity Framework Core with Npgsql, PostgreSQL, Bootstrap 5.3, custom CSS and lightweight vanilla JavaScript, xUnit, ASP.NET Core integration testing, Testcontainers for PostgreSQL, Docker Compose.

---

## 1. Confirmed Scope

### Customer-facing features

- Responsive home page with hero content, featured categories, featured products, and merchandising sections.
- Product catalog with keyword search, category filtering, price filtering, availability filtering, sorting, and pagination.
- Product detail page with gallery-style local imagery, description, price, stock status, SKU, and add-to-cart controls.
- Anonymous and authenticated shopping carts with quantity changes, removal, subtotal, discount, and total.
- Account registration, login, logout, and validation through ASP.NET Core Identity.
- Guest checkout and authenticated checkout.
- Shipping/contact address capture.
- Simulated payment only; no payment gateway and no card data collection/storage.
- Discount-code application with validity, usage, and minimum-order checks.
- Atomic order placement, stock decrement, order confirmation, and order number generation.
- Signed-in customer order history and order-detail views.

### Administration features

- Role-protected `/admin` area.
- Dashboard with catalog, inventory, customer, and order summaries.
- Category CRUD.
- Product CRUD, product activation/deactivation, featured flag, price, SKU, image selection, and stock updates.
- Order listing, filtering, details, and controlled status transitions.
- Discount-code CRUD and activation/deactivation.

### Dummy content

- Approximately 50 products across 8 categories for useful browsing variety.
- Local, repository-owned product/category imagery; the app must not depend on third-party image hosts.
- At least 3 seeded customer accounts and 1 seeded administrator.
- A realistic mix of seeded addresses, discount codes, stock levels, featured products, and previous orders.
- Demo credentials documented clearly as development-only data.

### Explicitly deferred

- Product reviews and ratings.
- Wishlists.
- Stock reservation before order placement.
- Real payments, card collection, refunds, shipment integrations, email delivery, and production hosting.
- Cloud deployment, production secrets management, monitoring, backups, and production hardening beyond sensible secure application defaults.

---

## 2. Design Choices and Rationale

### ASP.NET Core MVC with Razor views

This is a server-rendered monolith rather than a separate JavaScript SPA. It reduces moving parts, keeps validation and authorization close to the business logic, provides good first-load performance and SEO, and still supports a modern interface using Bootstrap, custom CSS, and small JavaScript enhancements.

### PostgreSQL in Docker

PostgreSQL avoids a host SQL Server installation and provides a production-grade relational database. Both the web application build/runtime and database will be containerized, so the missing host .NET SDK is not a blocker and no machine-wide SDK installation is required.

### One deployable application with internal boundaries

The app remains monolithic but avoids a single undifferentiated codebase. Controllers handle HTTP flow, services hold transactional business logic, EF Core entities represent persistence, view models define page contracts, and Razor views own presentation. This supports maintainability without premature clean-architecture layers or microservices.

### Server-side authority for commerce rules

Prices, discounts, stock, totals, roles, and order state transitions will be calculated and enforced server-side. Browser values are treated as untrusted. An order stores product name, SKU, unit price, discount, and address snapshots so historical records do not change when catalog data changes.

### Session-backed cart with checkout conversion

Anonymous users need a cart without an account. A session identifier will address the active cart; authentication can retain or merge that cart. Checkout converts the cart to an order in a database transaction. Because stock reservation is deferred, current stock is checked and decremented only during successful order placement.

### Modern responsive visual system

The interface will use a restrained neutral palette with a strong accent color, consistent spacing/type scales, rounded cards, clear focus states, responsive grids, compact mobile navigation, sticky cart feedback, helpful empty states, and accessible forms. Design tokens will live in CSS variables so the look remains coherent instead of being a collection of default Bootstrap components.

---

## 3. Proposed Repository Layout

```text
/
├── .dockerignore
├── .editorconfig
├── .env.example
├── .gitignore
├── docker-compose.yml
├── Dockerfile
├── EcommerceApp.sln
├── README.md
├── src/
│   └── EcommerceApp.Web/
│       ├── Areas/
│       │   └── Admin/
│       │       ├── Controllers/
│       │       ├── ViewModels/
│       │       └── Views/
│       ├── Controllers/
│       ├── Data/
│       │   ├── Configurations/
│       │   ├── Migrations/
│       │   ├── ApplicationDbContext.cs
│       │   └── DevelopmentDataSeeder.cs
│       ├── Domain/
│       │   ├── Entities/
│       │   └── Enums/
│       ├── Extensions/
│       ├── Services/
│       │   ├── Cart/
│       │   ├── Catalog/
│       │   ├── Checkout/
│       │   └── Discounts/
│       ├── ViewModels/
│       ├── Views/
│       ├── wwwroot/
│       │   ├── css/
│       │   ├── images/
│       │   └── js/
│       ├── Program.cs
│       ├── appsettings.json
│       └── appsettings.Development.json
└── tests/
    ├── EcommerceApp.UnitTests/
    └── EcommerceApp.IntegrationTests/
```

This is still one production application. Test projects do not create extra runtime services.

---

## 4. Core Data Model

- `ApplicationUser`: Identity user plus display name and created timestamp.
- `Address`: customer-owned reusable address; checkout also copies an immutable address snapshot into the order.
- `Category`: name, slug, description, image, display order, active flag.
- `Product`: category, name, slug, SKU, short/full descriptions, price, stock quantity, image paths, featured/active flags, timestamps.
- `Cart` and `CartItem`: session/user ownership, product, quantity, timestamps.
- `DiscountCode`: code, percentage or fixed discount, minimum subtotal, maximum discount, start/end time, active flag, optional usage limit.
- `DiscountRedemption`: discount/user-or-order association for usage enforcement.
- `Order`: public order number, optional customer ID, guest contact email, address snapshot, monetary totals, discount snapshot, payment state, order status, timestamps.
- `OrderItem`: product reference plus immutable SKU/name/unit-price/quantity snapshots.

Important database constraints will include unique category/product slugs, unique SKU, unique normalized discount code, non-negative prices/stock, valid quantities, suitable decimal precision, indexes for catalog queries, and concurrency protection for stock-sensitive writes.

---

## 5. Delivery Milestones

### Milestone 0 — Verified scaffold and infrastructure (not user-testable MVP)

**Outcome:** The solution builds in Docker, PostgreSQL starts, migrations apply, tests execute, and the application health endpoint responds.

### Milestone 1 — Browsable storefront vertical slice

**Outcome:** A user can open a polished home page, browse realistic seeded products, filter/search/sort the catalog, and view product details on desktop and mobile.

This is the first useful browser handoff.

### Milestone 2 — Cart, accounts, and checkout vertical slice

**Outcome:** A guest or registered user can manage a cart, apply a valid discount, complete simulated checkout, and receive an order confirmation. Stock and totals are enforced in PostgreSQL.

### Milestone 3 — Customer history and complete administration

**Outcome:** Customers can see their orders. The seeded admin can manage catalog, stock, discounts, and order statuses through a protected responsive admin interface.

### Milestone 4 — Release-candidate verification

**Outcome:** Automated tests, fresh-start setup, accessibility/responsive browser checks, security checks, and documentation are complete. The app is ready for Karthik’s local testing.

---

## 6. Step-by-Step Implementation Plan

### Task 1: Establish repository-only development guardrails

**Files:** `.gitignore`, `.dockerignore`, `.editorconfig`, `.env.example`, `README.md`

1. Confirm every command uses `/home/karthik/Documents/Projects/eCommerceApp` as its working directory.
2. Initialize local Git only inside this folder.
3. Add ignores for build output, local secrets, IDE state, Compose data, test artifacts, and generated coverage.
4. Document that secrets and local environment files are never committed.
5. Verify Git’s top-level path equals this folder and the worktree contains only expected files.

### Task 2: Create the .NET solution and container workflow

**Files:** `EcommerceApp.sln`, `src/EcommerceApp.Web/*`, `tests/*`, `Dockerfile`, `docker-compose.yml`

1. Use a pinned .NET 10 SDK container to generate the MVC web project and xUnit projects; do not install a host SDK.
2. Add project references and test dependencies.
3. Create a multi-stage Dockerfile for restore, build, test-compatible source, publish, and non-root runtime execution.
4. Create Docker Compose services for `web` and `postgres`, health checks, named database storage, and environment-based connection settings.
5. Add a simple health endpoint.
6. Build the image, start the stack, verify PostgreSQL health and HTTP health, run all empty-suite tests, then stop cleanly.

### Task 3: Define and test the relational model

**Files:** `Domain/Entities/*`, `Domain/Enums/*`, `Data/ApplicationDbContext.cs`, `Data/Configurations/*`, unit/integration model tests

1. Write tests for monetary precision, required fields, unique identifiers, valid order/cart quantities, and entity relationships.
2. Add Identity user, catalog, cart, discount, address, and order models.
3. Configure constraints, indexes, delete behavior, decimal precision, timestamps, and optimistic concurrency where stock changes.
4. Generate the initial EF Core migration inside the container.
5. Apply it to a clean PostgreSQL database.
6. Assert the expected tables, keys, constraints, and indexes through integration tests.

### Task 4: Add development-only seeding and local imagery

**Files:** `Data/DevelopmentDataSeeder.cs`, `wwwroot/images/catalog/*`, seed integration tests, `README.md`

1. Create deterministic local category/product artwork with consistent aspect ratios and visual styling.
2. Define roughly 8 categories and 50 varied products with realistic names, descriptions, SKUs, prices, active/featured states, and stock levels.
3. Seed at least three customers and one administrator using ASP.NET Core Identity APIs, not direct password hashes.
4. Seed addresses, historical orders, several discount-code scenarios, and varied order statuses.
5. Make seeding idempotent and Development-environment-only.
6. Test repeated startup does not duplicate data and role assignments are correct.
7. Document demo credentials and emphasize they are local development accounts.

### Task 5: Build the shared responsive UI shell

**Files:** `Views/Shared/_Layout.cshtml`, partials, `wwwroot/css/site.css`, `wwwroot/js/site.js`, layout view models

1. Create CSS design tokens for color, typography, spacing, shadows, radii, focus indicators, and breakpoints.
2. Implement responsive header, search entry, category navigation, account controls, cart badge, mobile menu, footer, alerts, validation summaries, loading affordances, and empty-state components.
3. Keep JavaScript progressive and small; core navigation/forms must work without a SPA runtime.
4. Include CSRF support, semantic landmarks, labels, keyboard focus, reduced-motion behavior, and adequate color contrast.
5. Verify layout at representative phone, tablet, laptop, and wide-screen viewports with no console errors.

### Task 6: Implement the storefront catalog vertical slice

**Files:** `Services/Catalog/*`, `Controllers/HomeController.cs`, `Controllers/CatalogController.cs`, `ViewModels/Catalog/*`, `Views/Home/*`, `Views/Catalog/*`, tests

1. Write service/controller tests for active-product visibility, featured content, search, filters, sort options, pagination boundaries, and unknown slugs.
2. Implement composable, database-executed catalog queries with read-only tracking disabled.
3. Build the home page merchandising sections.
4. Build catalog cards, filter controls, active-filter summary, pagination, results count, and meaningful no-result state.
5. Build product detail pages with inventory state and quantity limits.
6. Verify real seeded-data browsing in a browser on mobile and desktop.

**Milestone 1 gate:** Build, test, migration, HTTP, responsive rendering, links, form behavior, accessibility basics, and console-error checks must pass before inviting user testing.

### Task 7: Implement registration, login, and authorization

**Files:** Identity configuration in `Program.cs`, account views/configuration, authorization tests

1. Configure Identity password, lockout, cookie, and role behavior for a local development app.
2. Scaffold or create branded Razor account pages consistent with the storefront.
3. Add return-URL validation and access-denied behavior.
4. Protect customer order history and all admin routes.
5. Test anonymous/authenticated/admin authorization boundaries and CSRF rejection.

### Task 8: Implement persistent anonymous/authenticated carts

**Files:** `Services/Cart/*`, `Controllers/CartController.cs`, `ViewModels/Cart/*`, `Views/Cart/*`, tests

1. Write tests for adding, updating, removing, stock caps, invalid quantities, inactive products, price changes, cart isolation, and authentication cart merge/retention.
2. Implement opaque session cart identity and user association.
3. Calculate all prices and totals server-side from current database records.
4. Build responsive cart UI with quantity controls, remove action, subtotal, discount placeholder, and empty state.
5. Update the shared cart badge from authoritative server data.
6. Exercise concurrent sessions to prove carts remain isolated.

### Task 9: Implement discount codes

**Files:** `Services/Discounts/*`, cart/checkout controller updates, tests

1. Write tests for case-insensitive matching, date windows, active flag, minimum subtotal, percentage/fixed amounts, maximum discount, usage limit, and invalid-code messaging.
2. Centralize discount evaluation in one service shared by cart preview and checkout.
3. Persist the selected code safely while revalidating it during order placement.
4. Display transparent subtotal, discount, and total calculations.

### Task 10: Implement guest and authenticated checkout

**Files:** `Services/Checkout/*`, `Controllers/CheckoutController.cs`, checkout view models/views, tests

1. Write tests for required contact/address fields, empty carts, changed prices, insufficient stock, disabled products, invalid discounts, idempotent form submission, and transaction rollback.
2. Build a clear mobile-first checkout form for contact, shipping address, order summary, and a simulated payment choice.
3. Do not request or store card numbers, CVVs, or real payment credentials.
4. In one PostgreSQL transaction, reload authoritative cart/product data, validate stock and discounts, create immutable order snapshots, decrement stock, record redemption, and clear the cart.
5. Generate non-sequential public-facing order numbers while retaining an internal primary key.
6. Build confirmation and failure/retry pages that do not leak internals.
7. Test two competing checkouts for the final unit of stock; exactly one may succeed.

**Milestone 2 gate:** A real browser run must complete both guest and registered checkout against PostgreSQL, verify the resulting database state, refresh confirmation safely, and show correct stock/cart/order totals.

### Task 11: Implement customer order history

**Files:** `Controllers/OrdersController.cs`, order view models/views, tests

1. Test that users can see only their own orders and guests cannot enumerate orders.
2. Build responsive order list and detail pages with statuses, address snapshot, item snapshots, and totals.
3. Add a secure confirmation lookup appropriate to the just-completed guest checkout without exposing predictable IDs.
4. Verify seeded and newly created order histories.

### Task 12: Implement the administration area

**Files:** `Areas/Admin/Controllers/*`, `Areas/Admin/ViewModels/*`, `Areas/Admin/Views/*`, admin CSS as needed, tests

1. Write authorization tests proving non-admins cannot access or mutate admin endpoints.
2. Build the dashboard and responsive admin navigation.
3. Implement category CRUD with slug/uniqueness validation and safe delete rules.
4. Implement product CRUD with SKU/slug uniqueness, price/stock validation, image allow-list selection, active and featured controls.
5. Implement discount-code CRUD with coherent dates/limits and normalization.
6. Implement order search/filter/detail and an explicit status-transition policy; prevent arbitrary or backward-invalid transitions.
7. Add antiforgery protection, validation, not-found handling, and success/error feedback to every mutation.
8. Browser-test core admin workflows and verify customer-facing results immediately reflect changes.

**Milestone 3 gate:** All customer ownership and admin role boundaries must pass integration tests and direct browser attempts from each seeded role.

### Task 13: Add observability and robust error handling

**Files:** `Program.cs`, error pages, logging configuration, tests

1. Add global exception handling and user-safe 404/500 pages.
2. Use structured logs without passwords, cookie values, personal addresses, or secret connection strings.
3. Add correlation support and health checks for app/database readiness.
4. Verify Production-mode errors do not expose stack traces or database details.

### Task 14: Complete automated and browser acceptance testing

**Files:** test projects, any browser-test scripts/configuration, test documentation

1. Run all unit and integration tests against an isolated Testcontainers PostgreSQL database.
2. Ensure tests use per-test records or transaction rollback and never perform unguarded destructive cleanup.
3. Test fresh database migration plus seed, repeated startup, and app restart with persisted Compose data.
4. Browser-test catalog, search/filter/sort, cart, discount, guest checkout, account checkout, order history, admin CRUD, status updates, logout, forbidden access, 404, and validation failures.
5. Test representative viewport widths and keyboard navigation.
6. Inspect browser console/network failures and fix all application-caused errors.
7. Run a dependency vulnerability audit and review authorization, CSRF, cookie, model-binding, mass-assignment, concurrency, and secret-handling risks.

### Task 15: Finalize local developer experience and documentation

**Files:** `README.md`, `.env.example`, Compose/Docker files

1. Document prerequisites as Docker plus Docker Compose only.
2. Document exact build, start, stop, reset, migration, seed, and test commands.
3. Document URLs, demo users, admin access, simulated payment behavior, and known deferred scope.
4. Ensure startup waits for PostgreSQL readiness and migrations are applied through an intentional, testable workflow.
5. Start from a clean checkout/database volume using only README instructions.
6. Verify the final app, then leave services in a clearly documented state for user testing.

**Milestone 4 gate:** A clean local setup must produce a working populated storefront with passing tests and no undocumented manual repair steps.

---

## 7. Test and Validation Strategy

### Unit tests

- Money and total calculations.
- Discount eligibility and amount rules.
- Pagination/filter parsing.
- Order-status transition policy.
- Address/order snapshot mapping.
- Cart merge and quantity rules.

### PostgreSQL integration tests

- EF Core mappings, constraints, migrations, and indexes.
- Identity roles and authorization.
- Cart isolation and persistence.
- Checkout transactionality and stock concurrency.
- Discount usage enforcement.
- User ownership boundaries.
- Admin mutations and antiforgery behavior.
- Development seed idempotence.

### Browser acceptance checks

- Mobile and desktop storefront rendering.
- Search/filter/sort/pagination behavior.
- Anonymous cart and guest checkout.
- Registration/login/cart continuity and authenticated checkout.
- Order-history isolation.
- Admin catalog, inventory, discount, and status workflows.
- Keyboard usability, focus visibility, form errors, empty states, responsive overflow, console errors, and failed network requests.

### Final verification commands

The exact commands will be finalized from the generated Compose configuration, but the final workflow will provide equivalents of:

```bash
docker compose build
docker compose up -d --wait
docker compose run --rm tests
docker compose ps
curl --fail http://localhost:<documented-port>/health
docker compose down
```

All commands must run with this repository as the working directory and must not depend on files outside it.

---

## 8. Security Baseline

- ASP.NET Core Identity password hashing; no custom password storage.
- Secure, HTTP-only, same-site cookies with environment-appropriate HTTPS behavior.
- Role and ownership authorization enforced server-side.
- Antiforgery tokens on every state-changing browser form.
- Explicit view models/allow-lists to prevent over-posting.
- Server-side price, discount, stock, and total calculation.
- Parameterized EF Core queries and encoded Razor output.
- Validated local image references; no arbitrary file upload in v1.
- No card details or real payment secrets.
- No credentials committed to source; local demo credentials are Development-only.
- Production-style exception handling tested even though hosting is deferred.

---

## 9. Risks and Trade-offs

- **Feature breadth:** Catalog, checkout, Identity, and admin workflows form a substantial app. Milestone gates prevent a large unverified integration at the end.
- **Stock without reservation:** Two users can attempt to buy the last unit. Transactional checkout and concurrency tests will guarantee only one succeeds, but inventory is not held while a user browses checkout.
- **Guest history:** Guest users receive an immediate secure confirmation but do not get a durable account-based history unless they register; guessing order IDs must not reveal orders.
- **Local images:** Repository-owned illustrations avoid broken external links, though they are product-style demo artwork rather than commercial photography.
- **Container-only SDK:** Builds are reproducible and avoid host installation, with slightly slower initial restore/build times.
- **Development seed credentials:** Convenient for testing but intentionally unsuitable for any later public deployment; production deployment would require a separate hardening plan.

---

## 10. Approval Criteria

Implementation may begin once Karthik approves:

1. The confirmed/deferred feature boundary.
2. The MVC monolith plus PostgreSQL/Docker architecture.
3. The four user-visible delivery milestones.
4. Development-only seeded accounts and simulated payments.
5. Local-only delivery for now, with production hosting postponed.

No implementation beyond this plan file has been performed.