# Rebaz journey review — 27 October 2026

## Release status

Implemented locally; not committed, pushed or deployed. No production records or
schema were changed. No new migration or financial formula was added.

**The live `/simulations` 500 is not yet confirmed fixed.** The public message
`500 Internal Server Error` does not identify its exception. Render logs were not
accessible: browser automation stopped because it could not verify Chrome's URL.
The production error, deployed SHA and migration metadata still need inspection.

## Confirmed history findings

The real request path is `SavedSimulationPagesController.Index` →
`GetSimulationsHandler` → `PricingSimulationRepository.GetPagedAsync` → EF
materialization → `PricingSimulationResponse.FromSimulation` →
`Views/SavedSimulationPages/Index.cshtml`.

An isolated PostgreSQL database at the previous migration reproduces:

```text
Npgsql.PostgresException / SQLSTATE 42703
column p.AdditionalDecisionCost does not exist
PricingSimulationRepository.GetPagedAsync (ToListAsync)
GetSimulationsHandler.Handle
SavedSimulationPagesController.Index
```

The query fails before Razor rendering. The detail route fails at materialization
too. The existing `20261019153847_AddOptionalSimulationBusinessCosts` migration
restores both routes in that isolated database. Every original persisted column
is compared before/after to prove recovery does not rewrite financial history.

On the current schema, mixed pricing/discount/delivery history works with absent
optional metadata, omitted nullable JSON inputs, linking disabled, context-only
and allocation snapshots. Tests cover pagination, archive/delete filtering,
another business's records and 404 responses to cross-business detail URLs.
Product and expense edits do not rewrite the saved snapshots.

Do not assume this is Render's exception. See [SAVED_HISTORY_500.md](SAVED_HISTORY_500.md)
for sanitized evidence to collect and conditional recovery instructions. If Render
shows the same missing column and that migration is pending on its actual database,
apply that **existing** migration through the documented secure direct-Neon workflow,
after separately authorizing the production update. Do not reset the database or
return an empty list to conceal the error.

## UI changes and boundaries

- Price: minimum units remains prominent; the expected-sales impact is stated in
  money and the selected period. If expected sales are unknown, a same-sales
  illustration is explicitly labelled instead of claiming an overall forecast.
- Discount: additional units required to avoid a lower result leads the answer.
  Matching today's contribution is distinguished from predicting demand.
- Delivery: the result distinguishes per-order remainder from a full-period
  comparison; unknown order counts never become an overall better/worse verdict.
- What If: the owner's assumption and its monetary impact lead; responses remain
  conditional, with their existing breakdowns available.
- Tables, calculation explanations and shared-cost context/allocation use
  expandable details. Important below-cost warnings remain visible.
- Advanced period costs, illustrative decline and shared-cost linking are
  secondary. Linking still starts off and must be explicitly selected. Required
  delivery charges still require a value, including explicit zero if absent.
- Input examples use the business currency (IQD for an IQD business), and quantity
  and period-cost examples follow the selected day/week/month. They are help text,
  never autofilled financial values. Invalid controls inside collapsed sections
  open those sections and receive focus.
- Business Costs reuses the existing month/total/Add cost layout; its cost form
  now explains inclusion with one short double-counting example.
- Product and cost names remain owner-entered data. Routes, request contracts,
  tenant filters, role checks, antiforgery, explicit saves and concurrency are
  unchanged. Saved-history navigation starts new decisions at the existing launcher.

English, Arabic (`ar-IQ`) and Arabic-script Badini (`ku-Arab-IQ`, resource fallback
`ku`) are included. New Kurdish wording, particularly financial terminology,
still needs native Badini review; it has not been substituted with Sorani.

## Verification and reproduction

Docker's Linux engine is unavailable. The optional native test runner uses the
already-installed PostgreSQL binaries to create a **new** password-protected cluster
under TEMP, listening only on loopback at a dynamically selected port. It does not
use the installed database service or local/Neon application connection strings.
Each fixture creates its own uniquely named database; cleanup drops only databases
created by that fixture and stops this new cluster. Diagnostic files remain in TEMP.
Docker remains the default when this runner is not used.

```powershell
dotnet build --no-restore
pwsh -NoProfile -File tools/Run-NativePostgresTests.ps1 `
  -PostgresBin 'E:\Users\PC\Postgres\bin' -ResultName journey-final
```

Focused history run: **15 passed**. New result-presentation tests: **6 passed**.
The first full run passed 330 unit tests and 143 integration/browser tests; six
Discount browser tests failed on obsolete navigation selectors. Those tests now
use product → decision → Start simulation. The rerun of all six Discount cases
and three strengthened mixed-history cases passed (**9/9**).

Final verification: `dotnet build --no-restore` passed (0 errors; 10 existing
package-security warnings). The complete native PostgreSQL 18.4 run passed
**330 unit tests + 149 integration/browser tests = 479 passed, 0 failed, 0 skipped**.
The integration assembly includes 125 database-backed tests and 24 independent
browser presentation cases. The final result files are:

- `tests/FinSight.UnitTests/TestResults/journey-final_net10.0_20261027111809.trx`
- `tests/FinSight.IntegrationTests/TestResults/journey-final_net10.0_20261027112142.trx`

Test reports, screenshots and temporary cluster diagnostics are ignored artifacts,
not source files for deployment. The disposable cluster was stopped after the run.

Browser coverage uses Chromium against real local HTTPS/Kestrel/PostgreSQL for
the authenticated product, cost-entry, preview, save and reopen journeys. Separate
Razor presentation fixtures check layout, localized summaries and optional sections.
These are automated browser journeys plus screenshot inspection, not a live
production or Google-provider test. English, Arabic and Badini run at 1440px and
390px widths, with RTL/no-overflow checks, modal focus, Escape, recalculation and
explicit-save assertions.

Existing NU1902/NU1903 warnings for cryptography dependencies remain; packages were
not changed in this focused milestone.

## Short review checklist

1. Use a test business. Create a product, then add a monthly cost and review the
   Yes/No/Unsure inclusion explanation. Verify actual/estimated amounts separately.
2. In Try decisions select a product and Price, Discount or Delivery, then Start.
   Change day/week/month; examples must follow it without altering entered values.
3. Calculate, read the leading answer, expand details, close, edit and recalculate.
   Unknown delivery counts must show per-order information only.
4. Try What If and a response. Confirm costs remain unlinked unless explicitly
   enabled. Preview or closing must not save anything.
5. Explicitly save each supported simulation, open Saved simulations and reopen it.
   Edit product costs afterwards and confirm the snapshot remains unchanged.
6. Repeat with English, Arabic and Badini on desktop/mobile. Use a second business:
   it must not list the first business's records; direct access must return 404.
7. Before a live release, capture Render's actual exception and verify the deployed
   SHA/database migration history. This local success does not prove live recovery.

## Created or modified files

Earlier uncommitted history-investigation files were preserved and reused:

- `docs/DEPLOYMENT.md`
- `docs/SAVED_HISTORY_500.md`
- `src/FinSight.Api/Website/WebsiteExceptionFilter.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.BusinessCostSchema.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.SavedHistoryCompatibility.cs`
- `tests/FinSight.IntegrationTests/Infrastructure/FinSightApiFactory.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.CostLinking.cs`

This review also creates or modifies:

- `docs/USER_JOURNEY_REVIEW.md`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ar-IQ.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ku.resx`
- `src/FinSight.Api/Views/BusinessCostPages/Form.cshtml`
- `src/FinSight.Api/Views/DeliveryPages/Preview.cshtml`
- `src/FinSight.Api/Views/DiscountPages/Preview.cshtml`
- `src/FinSight.Api/Views/SavedSimulationPages/Index.cshtml`
- `src/FinSight.Api/Views/SimulationPages/Preview.cshtml`
- `src/FinSight.Api/Views/WhatIfPages/Index.cshtml`
- `src/FinSight.Api/Views/Shared/_DecisionResult.cshtml`
- `src/FinSight.Api/Views/Shared/_DeliveryResult.cshtml`
- `src/FinSight.Api/Views/Shared/_DiscountResult.cshtml`
- `src/FinSight.Api/Views/Shared/_WhatIfResult.cshtml`
- `src/FinSight.Api/Views/Shared/_LinkedCostResult.cshtml`
- `src/FinSight.Api/Views/Shared/_Layout.cshtml`
- `src/FinSight.Api/Views/Shared/_PeriodImpact.cshtml`
- `src/FinSight.Api/Views/Shared/_SimulationInputHelp.cshtml`
- `src/FinSight.Api/wwwroot/css/site.css`
- `src/FinSight.Api/wwwroot/js/simulation-guidance.js`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.Delivery.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.Discounts.cs`
- `tests/FinSight.IntegrationTests/DecisionWorkspacePresentationTests.Results.cs`
- `tests/FinSight.IntegrationTests/BusinessCostMigrationTests.cs`
- `tests/FinSight.IntegrationTests/DeliveryMigrationTests.cs`
- `tests/FinSight.IntegrationTests/Infrastructure/TestPostgresDatabase.cs`
- `tools/Run-NativePostgresTests.ps1`
