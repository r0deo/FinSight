# Business Costs repair and monthly workspace

## Failure diagnosis and production boundary

The authenticated-navigation regression test captures the actual logged
`Npgsql.PostgresException` with SQLSTATE `42P01` and missing relation
`BusinessCosts` when the database is at `20261013120101_AddWhatIfSimulations`.
The request returns HTTP 500, **not an empty month**. Applying the existing
migrations to that same disposable database makes the same signed-in request
return HTTP 200 and the genuine empty state. Routes, MediatR resolution, Razor
rendering, resources and mappings work against the migrated schema.

The earlier deployment guide omitted `20261013163254_AddBusinessCosts`; the
release checklist now explicitly covers it and the new review migration.
Production logs, migration history and the deployed release were not accessible
or inspected. Therefore this is a demonstrated missing-schema failure and repair,
not a claim that production's exception has been confirmed or production repaired.
Do not diagnose a different SQLSTATE by assumption. No database exception is
converted to an empty collection, and startup still does not migrate.

See [DEPLOYMENT.md — Business Costs 500 release checklist](DEPLOYMENT.md#business-costs-500-release-checklist)
for exact masked direct-Neon migration commands and deployment ordering. The
required new migration is `20261014125743_AddBusinessCostMonthReviews`. It adds
only `BusinessCostMonths` and leaves existing costs, products, Identity accounts,
and both simulation snapshot types unchanged. No production migration was run.

## Implemented behavior

- The existing BusinessCost entity, category enums, CQRS requests, tenant-safe
  repository and MVC forms are reused. The workspace selects a month, lists
  entries, offers create/edit and a separate delete-confirmation page. Empty
  months prominently say **Add your first cost** and explain unknown expenses.
- Existing name/category/amount/month/actual-or-estimated/note/inclusion inputs
  remain unchanged. Business currency comes from the authenticated business.
- Operating actual, estimated and recorded combined amounts exclude inventory.
  Category cards are ordered largest first; inventory actual/estimated amounts
  remain separate. The page explicitly avoids staffing/operating recommendations.
- Owners can declare a nonempty month reviewed/complete or reopen it. This is
  explicitly the owner's declaration, not accounting verification. Estimates and
  Unsure entries remain estimates and unresolved even after review.
- A cost create, edit, delete or move reopens affected months. Moving an entry
  reopens both its old and new month. Existing entries start unreviewed; no
  migration invents owner declarations.
- Month-over-month comparisons use recorded operating amounts; inventory changes
  are separate. Actual/estimated previous amounts are shown. Missing records in
  either month prevent comparison, not imply zero. A recorded zero baseline has
  an absolute difference but **no percentage**. Either month unreviewed or either
  containing estimates produces a caution, even if an owner has reviewed it.
- The checklist marks each category as recorded/check completeness or absent/check
  applicability. It does not certify completeness or assume absent costs are zero.
- A read-only adjustment accepts a **recorded operating category** and 0–100%
  with at most two decimals. It calculates category amount × assumed reduction,
  rounds the saving to two decimals away from zero at midpoints, and subtracts
  that rounded saving from recorded operating costs. It is labelled conditional,
  includes any category estimates, excludes inventory/unrecorded costs and never
  writes entries, review state, products or simulations. There is no new scenario
  persistence or second simulation engine.
- Cost pages link to Simulations and What If; existing monthly simulation context
  links back to the selected cost month. No overhead is allocated to a product,
  no materials are deducted a second time, and no business net-profit/break-even
  figure is manufactured. Historical simulation snapshots/calculators are unchanged.

## Authorization, consistency and endpoint contracts

All website routes retain the Website cookie policy and Owner/Manager roles.
BusinessId is never a trusted form field: Application always uses current user
context. Reads/updates/deletes filter both entry ID and BusinessId; another tenant's
entry is 404. All POSTs use the existing antiforgery validation. API JWT behavior
and API routes are untouched.

| Route | Behavior |
| --- | --- |
| `GET /business-costs?month=2026-10` | Selected workspace, currency, previous-month comparison |
| `GET/POST /business-costs/create` | Existing validated entry form/create; POST redirects to month |
| `GET/POST /business-costs/{id}/edit` | Existing form/update with entry Version |
| `GET/POST /business-costs/{id}/delete` | Confirmation then version-checked deletion |
| `POST /business-costs/review` | Owner only in both MVC and Application; month, version, reviewed |
| `POST /business-costs/adjustment` | Read-only form preview; Month, Category, Percent |

`BusinessCostMonth` uses `(BusinessId, Month)` as its primary key and a long EF
concurrency token. UTC review time and authenticated reviewer ID are stored. A
monthly read uses a PostgreSQL repeatable-read snapshot for the entries and the
review version together. Cost changes and monthly version/invalidation updates
are committed in the same EF transaction. A competing first-month insertion or
stale monthly/entry update returns the existing 409 conflict response and rolls
back the losing write. Each successful review/reopen submission with a current
version advances the version; repeating an old version is a conflict, not success.
An empty month cannot be declared complete. Review records are retained after
the last cost is deleted, but their declaration is cleared.

The selected month survives language switching from an adjustment POST result by
returning to its safe GET URL. The hypothetical result is intentionally transient.
The existing unsaved-form warning and safe-return allowlist remain in force.

## Verification

Executed locally with Docker PostgreSQL 17 disposable databases, not Neon:

- `dotnet build --no-restore`: success, zero compiler errors.
- `dotnet test --no-build --logger 'trx;LogFileName=business-costs-final.trx'`:
  198 unit tests and 54 integration/browser cases passed (252 total, no skips).
- `dotnet ef migrations has-pending-model-changes --no-build --project src/FinSight.Infrastructure --startup-project src/FinSight.Api`
  with `DOTNET_ENVIRONMENT=Development`: no pending model changes.
- PostgreSQL authenticated navigation, missing-schema regression/recovery, empty
  state, CRUD, monthly filtering, actual/estimated/category/inventory totals,
  owner-only review, cross-tenant read/write denial, antiforgery, invalid input,
  two-context concurrency/rollback, review invalidation and non-persisting previews.
- Upgrade testing preserves old costs and both types of historical simulation
  snapshots. The full suite includes existing simulation, authentication and
  localization regressions/resource-completeness checks.
- Six real Chromium journeys: en, ar-IQ, ku-Arab-IQ at 1440px and 390px. They log
  in, use authenticated navigation, create/edit costs, review, enter an assumption
  (including Arabic-Indic digits), switch language/month, and confirm deletion.
  They check RTL/LTR, horizontal overflow, persisted values and no unintended
  simulation changes. Screenshots are under the ignored integration test output
  `bin/Debug/net10.0/TestResults/browser`; selected desktop/mobile images were
  inspected visually.

Build reports existing NuGet security advisories for transitive
`System.Security.Cryptography.Pkcs` and `System.Security.Cryptography.Xml` 10.0.12
in the test-project dependency graphs (NU1902/NU1903). No package versions were
changed in this feature; dependency remediation needs a separate reviewed update.
No production checks, deployment, commit or push were performed.

English, simple Arabic and existing Badini Arabic-script Kurdish are supplied for
all new messages. **Badini wording still needs a native Duhok-speaker review**,
especially operating costs, owner declaration, estimates and conditional savings.
Automated completeness/RTL tests cannot verify translation quality.

## Short operator check

1. Apply/verify the two required cost migrations using the deployment checklist,
   then release the matching application version through your approved workflow.
2. Sign in as Owner; open Business costs. Pick an empty month and add rent, an
   estimated energy cost, and inventory. Check separate totals and your currency.
3. Mark the month reviewed. Edit a cost, move it to another reviewed month, and
   confirm deletion. Verify affected months reopen and stale forms show 409.
4. Compare with an absent month, a zero recorded baseline and a reviewed month
   containing estimates. Check the warnings and lack of invalid percentages.
5. Enter a category reduction; confirm its conditional result and unchanged data.
   Switch languages and test mobile layout. Have a native speaker review Badini.
6. Sign in as another business and as a Manager. Verify tenant denial and that
   Managers can manage entries but cannot declare a month reviewed.

## Changed and created files

- `docs/DEPLOYMENT.md`
- `docs/BUSINESS_COSTS_WORKSPACE.md`
- `src/FinSight.Domain/Entities/BusinessCostMonth.cs`
- `src/FinSight.Application/Common/Interfaces/IBusinessCostRepository.cs`
- `src/FinSight.Application/Features/BusinessCosts/BusinessCostRequests.cs`
- `src/FinSight.Application/Features/BusinessCosts/MonthlyCostWorkspace.cs`
- `src/FinSight.Infrastructure/Persistence/FinSightDbContext.cs`
- `src/FinSight.Infrastructure/Persistence/Configurations/BusinessCostMonthConfiguration.cs`
- `src/FinSight.Infrastructure/Persistence/Repositories/BusinessCostRepository.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/20261014125743_AddBusinessCostMonthReviews.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/20261014125743_AddBusinessCostMonthReviews.Designer.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/FinSightDbContextModelSnapshot.cs`
- `src/FinSight.Api/Website/Controllers/BusinessCostPagesController.cs`
- `src/FinSight.Api/Website/Models/BusinessCostViewModels.cs`
- `src/FinSight.Api/Views/BusinessCostPages/Index.cshtml`
- `src/FinSight.Api/Views/BusinessCostPages/_AdjustmentForm.cshtml`
- `src/FinSight.Api/Views/Shared/_Layout.cshtml`
- `src/FinSight.Api/wwwroot/css/site.css`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ar-IQ.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ku.resx`
- `tests/FinSight.UnitTests/Application/Features/BusinessCosts/BusinessCostHandlerTests.cs`
- `tests/FinSight.UnitTests/Application/Features/BusinessCosts/MonthlyCostWorkspaceTests.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.BusinessCostSchema.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.MonthlyCostWorkspace.cs`
- `tests/FinSight.IntegrationTests/BusinessCostMigrationTests.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.cs`
