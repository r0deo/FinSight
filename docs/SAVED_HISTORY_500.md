# Saved simulation history: verified local failure and recovery

Investigation date: 2026-10-20. Application baseline: `ce84afe8711084d08f027ba30ba2b8d54243822d`.

## What is confirmed, and what is not

The current application reproduces HTTP 500 on an isolated PostgreSQL database
whose latest migration is `20261019084302_AddDeliverySimulationSnapshot`.
The actual server exception captured through `WebsiteExceptionFilter` is:

```text
Authenticated GET /simulations
Npgsql.PostgresException
SQLSTATE 42703
column p.AdditionalDecisionCost does not exist

NpgsqlCommand.ExecuteDbDataReaderAsync(...)
EntityFrameworkQueryableExtensions.ToListAsync(...)
PricingSimulationRepository.GetPagedAsync(...) : line 72
GetSimulationsHandler.Handle(...) : line 15
ValidationBehavior.Handle(...)
SavedSimulationPagesController.Index(...) : line 19
```

`GET /simulations/{id}` reproduces the same missing-column error. Its stack
continues through `GetSavedDecisionHandler.Handle` and
`SavedSimulationPagesController.Detail`. Full captured traces are in the ignored
test result output, not in public error pages.

Applying **the already-existing**
`20261019153847_AddOptionalSimulationBusinessCosts` migration to that disposable
database restores both routes to HTTP 200. The original row remains present
with the same prices, unit cost, contributions, difference, timestamps and version.
The newly added nullable fields remain null on that row.

This proves a schema-mismatch failure and its recovery **locally**. It does not
establish that Render has that same exception. Production logs, deployed commit,
and production schema/migration history have not been read or changed in this task.
No conclusion of lost saved data is supported.

## Request trace

Listing:

1. Authenticated navigation points at `GET /simulations`.
2. `SavedSimulationPagesController.Index` sends
   `GetSimulationsQuery(page, 20, includeArchived, true)`.
3. `GetSimulationsHandler` calls `PricingSimulationRepository.GetPagedAsync`
   with the authenticated BusinessId.
4. The repository filters the tenant, deleted state and optional archived state,
   counts rows, then orders by creation timestamp and ID and applies pagination.
5. EF materializes all mapped columns, including `AdditionalDecisionCost` and
   `CostContext`. A missing column fails here, before response mapping or Razor.
6. On a compatible schema, `PricingSimulationResponse.FromSimulation` maps
   the rows and `Views/SavedSimulationPages/Index.cshtml` renders them.

Details:

1. `GET /simulations/{id}` reaches `SavedSimulationPagesController.Detail`.
2. `GetSavedDecisionHandler` performs a tenant-safe, non-deleted lookup.
3. Discount/delivery use their saved snapshots. Pricing uses saved inputs and
   authoritative saved totals, not current product prices or expenses.
4. `Views/SavedSimulationPages/Detail.cshtml` chooses the corresponding result
   partial, then the shared business-cost context partial.

The two newly mapped context columns and additional-decision-cost column are
nullable, but **nullable does not mean that the database column may be absent**.
No query, record filtering, calculation or financial formatting was changed
to conceal a missing schema.

## Changes made locally

- Added regression coverage that starts a **fresh disposable database** at the
  previous migration, seeds an existing row, captures the actual HTTP 500
  exception/stack, applies the existing migration and proves recovery without
  rewriting historical financial values.
- Added mixed-history tests across English, Arabic and Badini: 21 records across
  two pages, classic pricing with absent optional metadata, modern pricing,
  discount and delivery; linking off, context-only and allocated; null quantities
  and costs; older JSON with nullable optional properties omitted.
- Added nine authenticated save → list → details journeys across all three
  decision types and all three linking modes. They verify preview is read-only,
  product/expense changes do not rewrite saved snapshots, and another business
  sees neither the history nor the details.
- Extended the existing desktop/mobile browser journey to open a saved record
  from the history list, not only from the save redirect.
- Added method, path and trace ID to the existing server-side exception event.
  This event omits query strings, bodies, credentials, cookies and authorization
  headers. Public error text remains generic.

Changed files:

- `src/FinSight.Api/Website/WebsiteExceptionFilter.cs`
- `tests/FinSight.IntegrationTests/Infrastructure/FinSightApiFactory.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.BusinessCostSchema.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.SavedHistoryCompatibility.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.CostLinking.cs`
- `docs/SAVED_HISTORY_500.md`
- `docs/DEPLOYMENT.md`

## Production evidence needed

Provide only sanitized diagnostic information:

- Exact failing page path and query options (for example `/simulations?page=1`
  or `/simulations/<record-id>`), without tokens or credentials.
- Failure time and timezone, and Render request/trace ID if available.
- The commit SHA actually deployed in Render, not just the latest GitHub SHA.
- The corresponding server error: exception type, SQLSTATE, missing column/table
  name if any, and the relevant stack frames.
- Whether migration history includes
  `20261019153847_AddOptionalSimulationBusinessCosts` on the branch/database used
  by the running service.

Do not share environment-variable values, full connection strings, JWTs,
certificate contents, passwords, request bodies or customer records.
If the error differs from the reproduced signature, investigate that exception;
do not assume running a migration will solve it.

## Recovery procedure if the production mismatch is confirmed

No new migration is needed. No data reset, record deletion, migration-history
editing or financial recalculation is needed.

1. Verify the Render runtime's pooled connection and your DIRECT migration
   connection refer to the same intended Neon branch/database. Do not expose
   either connection string.
2. Inspect migration metadata read-only, for example through Neon's SQL console:

   ```sql
   SELECT "MigrationId"
   FROM "__EFMigrationsHistory"
   ORDER BY "MigrationId";

   SELECT table_name, column_name
   FROM information_schema.columns
   WHERE table_schema = 'public'
     AND ((table_name = 'PricingSimulations'
           AND column_name IN ('AdditionalDecisionCost', 'CostContext'))
          OR (table_name = 'WhatIfSimulations' AND column_name = 'CostContext'))
   ORDER BY table_name, column_name;
   ```

3. If the migration is pending, use a checkout containing it. After separately
   authorizing the database update, follow the secure masked DIRECT-Neon process
   in [DEPLOYMENT.md](DEPLOYMENT.md#2-explicit-migrations-before-launch).
   Rebuild the migration helper from that checkout; an old compiled helper may
   not contain the new migration:

   ```powershell
   dotnet build tools/FinSight.NeonMigrate/FinSight.NeonMigrate.csproj
   dotnet tools/FinSight.NeonMigrate/bin/Debug/net10.0/FinSight.NeonMigrate.dll
   ```

   This helper performs writes only after you enter the direct URL securely and
   confirm the displayed target with `APPLY`. Do not run it as a read-only probe.
   It applies pending migrations and verifies their persisted history.

   If using the environment-configured EF workflow instead, after securely
   setting the intended DIRECT connection as described in DEPLOYMENT.md:

   ```powershell
   dotnet ef migrations list --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
   dotnet ef database update --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
   ```

4. Verify the migration is recorded and all three columns exist. If history says
   it is already applied but columns are absent, stop and investigate schema drift
   or the wrong database/schema; do not fake a migration-history row.
5. Ensure Render runs the compatible application commit, then retest. A missing
   migration can be repaired without a new application release when that commit
   is already running. The diagnostic logging enhancement in this patch would
   require a later authorized deployment.

## Live verification

- Sign in and open `/simulations` through Saved History. Check both an older and
  a recently saved record, including discount/delivery.
- Check page 2 if populated and the include-archived filter.
- Preview then explicitly save with linking off, and with linking on; open each
  through the history list. Check that the saved result matches its preview.
- Use a second business: its empty history should be a real empty state; another
  business's record URL should return 404, not 500.
- Verify no new matching exception appears in Render logs. Do not edit or delete
  customer records just to test recovery.

## Verification status

Reverified on 27 October 2026 against application baseline `085ef41` plus the
local journey-review changes. Docker was unavailable, so verification used a new,
password-protected native PostgreSQL 18.4 cluster with one disposable database per
fixture. The full suite passed: 330 unit tests and 149 integration/browser tests.
The mixed-history regression now explicitly stores a record for Business B and
checks both businesses' lists and cross-business 404 responses.

Build passed. See [USER_JOURNEY_REVIEW.md](USER_JOURNEY_REVIEW.md) for the exact test
commands, result files, changed files and UI verification. The Render exception,
deployed commit and production schema remain unverified. No production migration,
data change or deployment was performed.
