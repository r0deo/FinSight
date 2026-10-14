# Business costs and progressive What If

## What changed

What If is a secondary simulation tool, not a global navigation item or a home/product card.
Open **Simulations → What if conditions change?**. The pricing and What If links carry the
selected product ID. Existing `/what-if/{id}` history URLs are unchanged.

Choose supplier prices, fewer units, or both. Only relevant assumptions are submitted;
the server also discards stale inputs for inactive concerns/modes and unchecked responses.
Illustrative buttons are explicitly not predictions. Response controls appear after a
result. The impact is shown before response controls. Changing assumptions hides the
old result/save action and asks for recalculation.

The existing calculators remain authoritative. The baseline 4,000 price / 1,000 cost /
100 units gives 300,000 contribution. A 20% cost increase and 10% quantity decrease
gives 252,000. At an alternative 4,500 price and 90 units, contribution is 297,000:
45,000 recovered, 3,000 short of baseline, and a maintenance threshold of 91 units.
Recovery language applies only to a positive original baseline, a shock decrease, and
a response recovering some/all of that decrease without overshooting. Other outcomes
are described as above/below/equal, with signed improvement versus no action.
Negative amounts and below-cost prices have explicit warnings. Nothing is called net profit.

## Routes and permissions

All routes below require the existing website cookie and Owner or Manager role.
POST mutations validate antiforgery; absent authentication redirects to website login.
API JWT endpoints and their 401/403 behavior are unchanged.

| Route | Purpose |
| --- | --- |
| GET /business-costs?month=2026-10 | Current business's month, category totals, entries and guidance |
| GET /business-costs/create?month=2026-10 | Add form |
| POST /business-costs/create | Create one monthly entry |
| GET /business-costs/{id}/edit | Tenant-safe edit form |
| POST /business-costs/{id}/edit | Update with the form's Version |
| GET /business-costs/{id}/delete | Confirmation page; does not delete |
| POST /business-costs/{id}/delete | Delete with Version |
| GET /what-if?productId={id} | Concern/assumption form |
| POST /what-if/preview | Read-only calculation |
| POST /what-if/save | Explicit immutable snapshot save |
| GET /what-if/history | Existing paginated saved scenarios |
| GET /what-if/{id} | Existing tenant-safe snapshot read |

Cost fields: Name (required, 100 characters), Category (one of eight categories),
Amount (0–999,999,999,999.99, at most two decimals), Month (YYYY-MM, 2000–2100),
Certainty (Actual/Estimated), Inclusion (Yes/No/Unsure), Note (optional, 500 characters).
Amount uses the business currency; the request cannot change it. Numeric fields use
the existing unambiguous website parser, including Arabic/Persian digits.

The name is also the custom label for Other. Every entry belongs to a specific month.
There is no automatic recurring charge or copy-month feature.

## Architecture and accounting boundaries

- Domain owns BusinessCost validation and private setters; enums live in Domain/Enums.
- Application owns CQRS requests, FluentValidation, responses/summary, and the repository interface.
- Infrastructure owns EF mapping and tenant-filtered persistence.
- MVC maps form fields to requests through MediatR. BusinessId always comes from authenticated context.
- Read-only queries use AsNoTracking. Update/delete load tracked rows with both ID and BusinessId.
- A stale version returns 409. EF also predicates UPDATE/DELETE on Version, so a simultaneous write
  between loading and saving cannot silently overwrite data.
- Cross-business read, edit and delete return 404 without disclosing the record.
- Inventory purchases are separate from operating categories; actual and estimated portions stay separate.
- Inclusion Yes/No/Unsure remains visible per entry; unresolved entries are counted. No cost is
  deducted from contribution, so included costs cannot be charged twice by this feature.
- An empty month means missing entered data, not evidence of zero real costs.
- Category ordering uses entered actual + estimated amounts for ranking only, with components shown.
  It is not a combined inventory/expense/profit/cash-flow total.

For a **monthly** simulation, an optional calendar month displays current business-wide entered
costs in a separate expandable context. Daily/weekly simulations never show that context.
It is labelled current entered information, not a saved calculation input or snapshot.
It is not saved, allocated to the product, or deducted from any result. Saved simulations do not
read current costs, so changing/deleting cost entries or editing products cannot revise history.
A whole-business operating result would require sales and costs across the business for the same period.

What If duplicate submissions use an ID derived from the validated user/tenant-bound preview proof.
A repeat returns the same record; the primary key prevents simultaneous duplicates (the losing racing
request receives a conflict and can reopen history). A fresh preview intentionally permits a new snapshot.
A price/cost change before first save still requires recalculation. Ordinary pricing API contracts are unchanged.

## Migration and verification commands

New migration: `20261013163254_AddBusinessCosts`. It adds one table and its tenant/month index;
it does not rewrite Products, PricingSimulations or WhatIfSimulations.

Only disposable Testcontainers databases were migrated by this milestone's automated tests.
No production migration, seed, commit, push or deployment is part of this work.

For an explicitly intended **local development** database, first confirm the environment connection
override is not pointing to Neon, then run:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet ef database update --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --context FinSightDbContext
```

For any future production release, follow the secure direct-connection procedure in DEPLOYMENT.md.
Migrations are never applied on normal application startup.

```powershell
dotnet build
powershell -NoProfile -ExecutionPolicy Bypass -File tests/FinSight.IntegrationTests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --context FinSightDbContext
```

Docker must be running. Microsoft.Playwright 1.63.0 is a test-only package.
The browser fixture uses real HTTPS Kestrel, disposable PostgreSQL, isolated confirmed test accounts,
and real password/cookie login. It does not contact Google, email providers, or Neon.
Screenshots go under the ignored integration-test bin/TestResults/browser directory.
Test fixtures use synthetic credentials and an ephemeral self-signed TLS certificate, never production secrets.

See MILESTONE_VERIFICATION.md for the executed results and OWNER_USABILITY_TEST.md for the
moderated acceptance script. Automated behavior checks do not prove customer usefulness.

## Manual acceptance and limitations

1. In each language, check global navigation/home have no What If item, then open it from Simulations.
2. Pick a product, try each concern, inspect relevant fields and the illustrative labels.
3. Run the reference case; compare a price response and explain the assumptions before saving.
4. Change an assumption: the old save/result must disappear until recalculation.
5. Add actual, estimated, inventory, included and unsure costs in two months; edit and confirm-delete.
6. Choose Month on a simulation and select a matching month. Costs are context only; contribution stays unchanged.
7. Open two copies of a cost edit form; the second stale save must conflict.
8. Sign in as a different business and try the first business's cost/scenario URL: expect 404.
9. Reopen saved history after product/cost edits: calculations remain unchanged.

English, Arabic (ar-IQ), and Badini/Kurmanji Arabic script (ku-Arab-IQ, ku resources) are provided.
New Kurdish wording needs native Duhok review, especially contribution, recovered decrease,
unit-cost inclusion, estimated costs and inventory consumption. Do not treat it as Sorani approval.
No accounting, stock consumption, payroll, tax, automatic allocation, recurring expenses,
or new archiving/deletion behavior for What If snapshots was added.
