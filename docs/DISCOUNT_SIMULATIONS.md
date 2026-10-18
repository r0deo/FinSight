# Single-product discount simulations

## Scope and architecture

- Open **Simulations → Offer a discount**, or switch from a product's **Change selling price** preview. No new navbar item.
- Owner and Manager can select an active product from their own business. Every lookup uses the authenticated business; no submitted business/user identity is accepted.
- The Domain `DiscountSimulationCalculator` uses the original contribution calculator. Discounted price is rounded **first**, to two decimals, midpoint away from zero, matching the product-cost helper's monetary rounding. Every subsequent total, warning and threshold uses that same rounded price.
- Percentage must be strictly between 0 and 100, with at most four decimal places. Current quantity is a positive Int32; expected quantity is optional and non-negative. Monetary inputs allow at most two decimals and must fit `numeric(18,2)`; aggregate totals/differences outside that range are rejected. Thresholds beyond Int32 are explained rather than converted or overflowed.
- Campaign cost is for the same comparison period as sales. Null means excluded/unknown; explicit zero means no additional cost. Only enter costs not already present in the recorded product cost. Whole-business overhead is not allocated to this product.
- No expected quantity means no scenario total, difference or recommendation. Positive scenario text is conditional on the user's sales assumption. Contribution is never labelled net profit.
- Normal maintenance thresholds require a positive baseline and positive discounted unit contribution. Below-cost, zero-margin, non-positive baseline and out-of-range thresholds have distinct explanations.
- Application CQRS requests use FluentValidation and existing repositories. Preview has no persistence dependency. Previewing an edit reads the original product financial snapshot, not today's product.
- Explicit save uses the existing `PricingSimulation` aggregate/table and history, not a second simulation repository. `DiscountSnapshot` is an immutable inputs/results/version record stored as nullable JSON text, consistent with the existing What If snapshots. Non-null identifies a discount; null remains the existing selling-price simulation. Period, product name and currency remain on the existing aggregate.
- Create checks that product price/cost still match the preview and recalculates server-side. No result from a form is trusted. Edit preserves original price/cost and replaces assumptions/results only through the discount Domain method.
- Reads return the stored discount result without recalculating it. Product financial edits, soft deactivation, archive, delete and restore never rewrite that snapshot.
- Discount updates use the existing version token and EF concurrency predicate. Two independently tracked writes with the same version cannot both succeed. Classic price-update commands cannot overwrite a discount record.
- Classic JWT preview/read/list DTOs are unchanged. Discount records are intentionally excluded from the classic read/list contract (as unknown-expected-sales decisions already were); the website uses existing full history queries. Existing JWT archive/unarchive/delete/Owner-only restore endpoints still operate on the aggregate with its version. This milestone does not add a new public discount REST API or redesign lifecycle screens.

## Website routes

- `GET /simulations/discount`: paginated active-product chooser.
- `GET /simulations/discount?productId=<id>`: discount form.
- `POST /simulations/discount`: read-only preview.
- `POST /simulations/discount/save`: explicitly save, then redirect to existing `/simulations/<id>`.
- `GET/POST /simulations/<id>/discount/edit`: saved-input editor / read-only recalculation.
- `POST /simulations/<id>/discount/save`: explicit versioned update.

All these pages require website Owner/Manager authentication. All POSTs use antiforgery. The shared result modal provides keyboard focus containment, Escape/close, focus return, and scrolling. Input edits invalidate the old result's reopen control. Without JavaScript the same result and explicit save form remain available inline.

## Migration and deployment order

New migration: **`20261018141822_AddDiscountSimulationSnapshot`**.
It adds only nullable `PricingSimulations.DiscountSnapshot` text. Existing rows are preserved with null. No production database has been changed by this milestone.

Apply before starting the updated application. Old code tolerates the additional nullable column; new code needs it even when reading classic simulations. Do not roll back/drop the column after saving discounts, because that would lose their additional inputs and snapshot.

For your development database, after verifying the development connection and ensuring no environment connection override points elsewhere:

```powershell
cd D:\FinSight
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet ef database update 20261018141822_AddDiscountSimulationSnapshot --project src/FinSight.Infrastructure --startup-project src/FinSight.Api
dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api
Remove-Item Env:DOTNET_ENVIRONMENT
```

For a separately authorized Neon deployment, follow `docs/DEPLOYMENT.md`: build the existing `tools/FinSight.NeonMigrate` helper from this source, run it interactively, enter the **direct** Neon URL at its masked prompt, verify the intended host/database, and confirm application. Do not put credentials in command arguments, source, or chat. Verify the new migration appears in applied history before releasing the web image. No automatic startup migration is introduced.

## Manual verification

1. Use an active product priced at 5,000 IQD with recorded unit cost 3,000 IQD.
2. Simulations → Offer a discount → choose the product. Enter 10%, current quantity 100, expected quantity 140, campaign cost explicitly 0, and week.
3. Calculate: discounted price 4,500; current contribution 200,000; per-unit contribution 1,500; minimum 134; additional 34 / 34%; scenario 210,000; difference +10,000.
4. Close/Escape. Inputs should remain. Change expected quantity to 120 and recalculate: scenario 180,000, difference −20,000. Nothing is saved yet.
5. Use expected quantity 140 and campaign cost 30,000: minimum 154; scenario 180,000; difference −20,000.
6. Explicitly save and reopen in history. Change the product's financials; the saved discount still shows the original 5,000/3,000 snapshot. Edit the discount via its saved detail page; recalculate then explicitly save.
7. Leave expected quantity and campaign cost blank: threshold still shown, campaign exclusion visible, no scenario total/recommendation. Try 40% (zero margin), 50% (below cost), invalid zero/100% discounts, and current quantity 0.
8. Try another business's product and saved-record URLs: 404. Submit an old edit version: 409. Missing antiforgery token: 400. Archived/deleted records cannot be edited.
9. Repeat in English, Arabic and Badini, desktop and mobile. Use Arabic-Indic input digits and localized decimal separators without grouping separators. Currency and calculations must remain unchanged.

## Translation and verification notes

Supported cultures remain `en`, `ar-IQ`, `ku-Arab-IQ` (Badini/Kurmanji Arabic script, `ku` resource fallback). New Kurdish financial wording needs native Badini-speaker review before public rollout; it is not claimed as professionally reviewed.

Chromium automation and screenshots do not replace physical-device, other-browser, or screen-reader testing. No live Render/Neon deployment check is part of this local milestone.

## Executed verification (2026-10-18)

- `dotnet build --no-restore --verbosity quiet`: passed, zero errors. Ten existing NU1902/NU1903 cryptography dependency warnings remain; this milestone changes no packages.
- `dotnet test --no-build --no-restore --logger "console;verbosity=minimal"`: **363 passed, zero failed, zero skipped** (277 unit; 86 PostgreSQL/integration/browser).
- `dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build`: no pending model changes (design-time development configuration only).
- Existing PostgreSQL upgrade regression now seeds the old pricing schema explicitly rather than using today's mapping against an old table. It upgrades through the new migration, verifies original price/What If snapshots, and verifies null discount snapshots on legacy records.
- New authenticated HTTP tests run in all three languages: preview without persistence, reference calculations, null assumptions, explicit save/reopen, immutable product financial snapshot, edit preview without persistence, version conflict, forged identity ignored, cross-business read/edit/create rejected, antiforgery, archive/unarchive/delete/restore, and two independently tracked database writes using one version.
- Six new Chromium journeys (three languages × 1440/390 pixels): Manager access, product selection, localized/Arabic-Indic input, field error/focus, successful modal, close/Escape/reopen, focus trap, unchanged form values, stale-result invalidation, fresh calculation, explicit save, snapshot reopening and cancelled edit. Existing simulation/product/browser journeys also pass.
- Representative screenshots visually inspected: English desktop results, Arabic mobile inputs/results, and Badini desktop results. Automated layout checks covered all six combinations. Screenshots stay in ignored test output, not source assets.
- Early test runs caught English-only separator assertions and one full-page-load timeout in the new browser test. Expectations now use each requested culture and navigation waits use the existing suite's DOMContentLoaded convention. The final full suite passes.
- No local checks are blocked. Local development and production databases were not migrated; migration application during verification was confined to disposable test databases. No commit, push or deployment performed.

## Changed files

Domain:

- `src/FinSight.Domain/Entities/PricingSimulation.cs`
- `src/FinSight.Domain/Exceptions/DiscountValidationException.cs`
- `src/FinSight.Domain/Models/DiscountSimulationSnapshot.cs`
- `src/FinSight.Domain/Services/DiscountSimulationCalculator.cs`

Application:

- `src/FinSight.Application/DependencyInjection.cs`
- `src/FinSight.Application/Features/PricingSimulations/Discounts/DiscountRequests.cs`
- `src/FinSight.Application/Features/PricingSimulations/Discounts/DiscountHandlers.cs`
- `src/FinSight.Application/Features/PricingSimulations/PricingSimulationResponse.cs`
- `src/FinSight.Application/Features/PricingSimulations/Queries/GetSavedDecision/GetSavedDecisionQuery.cs`
- `src/FinSight.Application/Features/PricingSimulations/Queries/GetSavedDecision/GetSavedDecisionHandler.cs`

Infrastructure:

- `src/FinSight.Infrastructure/Persistence/Configurations/PricingSimulationConfiguration.cs`
- `src/FinSight.Infrastructure/Persistence/Repositories/PricingSimulationRepository.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/20261018141822_AddDiscountSimulationSnapshot.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/20261018141822_AddDiscountSimulationSnapshot.Designer.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/FinSightDbContextModelSnapshot.cs`

Website:

- `src/FinSight.Api/Website/Controllers/DiscountPagesController.cs`
- `src/FinSight.Api/Website/Controllers/CultureController.cs`
- `src/FinSight.Api/Website/Controllers/WebsiteController.cs`
- `src/FinSight.Api/Website/Models/DiscountViewModel.cs`
- `src/FinSight.Api/Website/Models/SavedSimulationViewModels.cs`
- `src/FinSight.Api/Views/DiscountPages/ChooseProduct.cshtml`
- `src/FinSight.Api/Views/DiscountPages/Preview.cshtml`
- `src/FinSight.Api/Views/Shared/_DiscountResult.cshtml`
- `src/FinSight.Api/Views/SimulationPages/Preview.cshtml`
- `src/FinSight.Api/Views/SavedSimulationPages/Index.cshtml`
- `src/FinSight.Api/Views/SavedSimulationPages/Detail.cshtml`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ar-IQ.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ku.resx`

Tests and handoff:

- `tests/FinSight.UnitTests/Domain/Services/DiscountSimulationCalculatorTests.cs`
- `tests/FinSight.UnitTests/Application/Features/PricingSimulations/DiscountHandlerTests.cs`
- `tests/FinSight.UnitTests/Application/Features/PricingSimulations/SavedPricingSimulationHandlerTests.cs`
- `tests/FinSight.UnitTests/Website/LocalizationTests.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.Discounts.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.Discounts.cs`
- `tests/FinSight.IntegrationTests/BusinessCostMigrationTests.cs`
- `docs/DISCOUNT_SIMULATIONS.md`
