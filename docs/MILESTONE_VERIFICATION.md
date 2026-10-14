# Milestone verification — 14 October 2026

## Final executed results

| Check | Result |
| --- | --- |
| dotnet build | Passed; 0 warnings, 0 errors |
| Unit tests | 181 passed, 0 failed, 0 skipped |
| Integration test assembly | 47 passed, 0 failed, 0 skipped |
| Total | 228 passed |
| Browser subset (included in the 47, not additional) | 6 passed: en / ar-IQ / ku-Arab-IQ, each at 1440px and 390px |
| EF pending-model-changes check | No pending model changes |
| git diff --check | Passed; only existing CRLF normalization notices |

The final run is recorded in ignored files:
- tests/FinSight.UnitTests/TestResults/milestone-final.trx
- tests/FinSight.IntegrationTests/TestResults/milestone-final.trx

Docker Desktop was initially stopped on the resumed run. That first browser attempt
could not initialize its containers. Docker was started locally and all final checks
were rerun successfully. There are no remaining Docker/browser blockers for this run.

## What the tests establish

- Independent numeric assertions for cost-only, demand-only and combined shocks;
  price increases/decreases, nonpositive baselines, zero/full-decline sales,
  below/equal cost, bounds, precision, floor/ceiling rounding, omitted inputs,
  and alternative outcomes above/below baseline.
- Real PostgreSQL preview-no-write, exact saved inputs/results, unchanged product
  financials, history independence, and valid other-business access returning 404.
- Existing simulation edit/archive/delete/restore/version and authentication regressions still pass.
- Monthly costs CRUD, category ordering/totals, actual/estimated separation,
  inventory exclusion from operating categories, visible inclusion uncertainty,
  month isolation, no trusted client tenant ID, antiforgery, role/authentication rules,
  and atomic EF concurrency conflicts with two independently tracked copies.
- A dedicated migration test creates the previous schema, inserts a representative
  business, user, product, legacy pricing simulation and What If snapshot, applies
  the new migration, and verifies their stored values plus a new cost entry.
- Browser tests use real password login, MVC, HTTPS Kestrel and disposable PostgreSQL.
  They create a product, preview/save/reopen pricing, enter What If via the selected
  product's simulation page, apply the reference shock, compare/save a response,
  add/edit costs, and inspect database records.
- Browser checks include first-result disclosure, checkbox controls, stale-result hiding,
  double-click suppression, validation 400 with visible errors, rejected proof 409
  without an extra saved row, correct lang/dir, no horizontal overflow and no raw
  formatting code. Happy paths have no JavaScript or console errors. Deliberate
  400/409 tests allow only Chromium's expected failed-HTTP-resource console messages.
- Screenshots were captured and representative desktop/mobile, English/Arabic/Badini
  screens were visually reviewed. Screenshots are under the ignored
  tests/FinSight.IntegrationTests/bin/Debug/net10.0/TestResults/browser directory.

## Bugs found and fixed

1. What If response controls used a name lookup that could return MVC's checkbox-plus-hidden
   field group instead of the checkbox. They now target the checkbox explicitly.
2. JavaScript quantity suggestions could suffer floating-point floor drift. They now
   scale two-decimal percentages before flooring; the decimal domain calculator remains authoritative.
3. Explicit-action What If forms lacked their own antiforgery inputs. Tokens are now
   explicitly generated; the new cost forms use the same protection.
4. MVC's empty validation summary emitted inline display:none styling, conflicting with
   the existing strict CSP. A shared tag helper suppresses empty summaries, without
   relaxing CSP or suppressing genuine validation errors.
5. Repeated What If saves could create duplicate snapshots. A validated preview yields a
   stable save ID, and the database primary key prevents racing duplicates.
6. Inactive concern/mode/response values could be stale. Server normalization ignores
   those values before validation/calculation; browser visibility is not the security boundary.
7. The capitalized Month label did not match the existing lowercase resource key.
   Cost pages now reuse the translated key.

The browser test fixture also reimports its generated test-only certificate in memory
for Windows Schannel compatibility. No production certificate/configuration was changed.

## Remaining human verification and intentional limits

- Run the 3–5-owner moderated script in OWNER_USABILITY_TEST.md. It is prepared but
  has not been conducted; no usefulness claim or participant feedback is fabricated.
- Native Duhok Badini review is still needed, particularly financial terminology.
- The suite uses Chromium, not a cross-browser/device lab. Google and real email delivery
  are outside this milestone and were not exercised.
- Monthly context is live entered information, explicitly separate from saved snapshots.
  No business-wide profit, inventory consumption, allocation, recurrence or stock accounting is implemented.
- No commit, push, deployment or production data change was performed.
- The new migration must be explicitly applied to the intended release database later.
  See BUSINESS_COSTS_AND_WHAT_IF.md for commands and the acceptance checklist.

## Changed and created files

- docs/BUSINESS_COSTS_AND_WHAT_IF.md
- docs/MILESTONE_VERIFICATION.md
- docs/OWNER_USABILITY_TEST.md
- docs/WHAT_IF.md
- src/FinSight.Api/Resources/Website/Localization/RebazText.ar-IQ.resx
- src/FinSight.Api/Resources/Website/Localization/RebazText.ku.resx
- src/FinSight.Api/Resources/Website/Localization/RebazText.resx
- src/FinSight.Api/Views/BusinessCostPages/Delete.cshtml
- src/FinSight.Api/Views/BusinessCostPages/Form.cshtml
- src/FinSight.Api/Views/BusinessCostPages/Index.cshtml
- src/FinSight.Api/Views/Home/Welcome.cshtml
- src/FinSight.Api/Views/ProductPages/Index.cshtml
- src/FinSight.Api/Views/SavedSimulationPages/Index.cshtml
- src/FinSight.Api/Views/Shared/_BusinessCostSummary.cshtml
- src/FinSight.Api/Views/Shared/_DecisionResult.cshtml
- src/FinSight.Api/Views/Shared/_Layout.cshtml
- src/FinSight.Api/Views/Shared/_SimulationCostContext.cshtml
- src/FinSight.Api/Views/Shared/_WhatIfResult.cshtml
- src/FinSight.Api/Views/SimulationPages/Preview.cshtml
- src/FinSight.Api/Views/WhatIfPages/Index.cshtml
- src/FinSight.Api/Views/WhatIfPages/SelectProduct.cshtml
- src/FinSight.Api/Views/_ViewImports.cshtml
- src/FinSight.Api/Website/Controllers/BusinessCostPagesController.cs
- src/FinSight.Api/Website/Controllers/CultureController.cs
- src/FinSight.Api/Website/Controllers/SimulationPagesController.cs
- src/FinSight.Api/Website/Controllers/WhatIfPagesController.cs
- src/FinSight.Api/Website/Models/BusinessCostViewModels.cs
- src/FinSight.Api/Website/Models/PreviewSimulationViewModel.cs
- src/FinSight.Api/Website/Models/WhatIfViewModels.cs
- src/FinSight.Api/Website/ValidationSummaryCspTagHelper.cs
- src/FinSight.Api/Website/WebsiteExceptionFilter.cs
- src/FinSight.Api/wwwroot/css/site.css
- src/FinSight.Api/wwwroot/js/simulation-preview.js
- src/FinSight.Api/wwwroot/js/what-if.js
- src/FinSight.Application/Common/Interfaces/IBusinessCostRepository.cs
- src/FinSight.Application/Features/BusinessCosts/BusinessCostRequests.cs
- src/FinSight.Application/Features/WhatIf/SaveWhatIfCommand.cs
- src/FinSight.Domain/Entities/BusinessCost.cs
- src/FinSight.Domain/Enums/BusinessCostCategory.cs
- src/FinSight.Domain/Enums/CostCertainty.cs
- src/FinSight.Domain/Enums/UnitCostInclusion.cs
- src/FinSight.Infrastructure/DependencyInjection.cs
- src/FinSight.Infrastructure/Persistence/Configurations/BusinessCostConfiguration.cs
- src/FinSight.Infrastructure/Persistence/FinSightDbContext.cs
- src/FinSight.Infrastructure/Persistence/Migrations/20261013163254_AddBusinessCosts.Designer.cs
- src/FinSight.Infrastructure/Persistence/Migrations/20261013163254_AddBusinessCosts.cs
- src/FinSight.Infrastructure/Persistence/Migrations/FinSightDbContextModelSnapshot.cs
- src/FinSight.Infrastructure/Persistence/Repositories/BusinessCostRepository.cs
- src/FinSight.Infrastructure/Persistence/Repositories/WhatIfSimulationRepository.cs
- tests/FinSight.IntegrationTests/BrowserJourneyTests.cs
- tests/FinSight.IntegrationTests/BusinessCostMigrationTests.cs
- tests/FinSight.IntegrationTests/FinSight.IntegrationTests.csproj
- tests/FinSight.IntegrationTests/WebsiteTests.BusinessCosts.cs
- tests/FinSight.IntegrationTests/WebsiteTests.cs
- tests/FinSight.UnitTests/Application/Features/BusinessCosts/BusinessCostHandlerTests.cs
- tests/FinSight.UnitTests/Domain/Entities/BusinessCostTests.cs
- tests/FinSight.UnitTests/Domain/Services/WhatIfCalculatorTests.cs
