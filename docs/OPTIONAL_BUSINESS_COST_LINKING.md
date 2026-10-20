# Optional Business Costs in financial simulations

## Delivered behavior

- Three shared destination cards: financial decisions, What If, saved history. Pricing, discount and delivery remain the decision cards within financial decisions. Product deep links, keyboard use and dirty-form confirmation are retained.
- Linking defaults OFF for every new calculation, even when recorded expenses exist. Allocation is independently OFF. Saved discount/delivery editors restore their own saved settings.
- Shared, localized form and result partials are used by pricing, discount, delivery and What If. Results remain in the existing accessible modal. Changing inputs/settings invalidates the modal's old result; neither preview nor closing saves.
- OFF performs no Business Costs query and stores no hidden overhead allocation. Explicit campaign, delivery and pricing decision costs remain included.
- ON loads a consistent month snapshot for the authenticated business through the existing Application repository abstraction. Submitted business IDs, expense rows and calculated amounts are not used.
- Eligible candidates are rent, wages, electricity/fuel and utilities marked **not included** in product unit costs. Inventory, transport, maintenance, other and included/unsure entries are excluded. All entries are visible in the included/excluded breakdown with category subtotals, certainty and review warnings.
- Existing records do not prove recurrence. Allocation requires the owner's explicit confirmation that included entries recur and do not overlap with unit/decision costs. This is a declaration, not automatic verification.
- Monthly comparisons use the selected recorded month without proration. Daily/weekly comparisons stay context-only. Missing records mean unknown costs, not zero. Incomplete delivery period inputs never allocate monthly costs against one order.
- Allocation accepts an owner-entered 0–100% with at most two decimals, without an invented default. The same rounded monetary amount is deducted from the baseline and every alternative. It cannot change the incremental benefit.
- Pricing adds one optional additional decision cost for the selected period. Blank remains unknown/excluded; explicit zero remains zero. Deduction occurs once in proposed scenarios only; minimum-sales thresholds include it. The result distinguishes amounts after product costs from amounts after decision costs.
- Discount campaign cost and delivery costs reuse existing fields. What If changes product costs/demand, not whole-business overhead; no second overhead-change engine or duplicate deduction was introduced.

## Snapshots and architecture

Domain holds classification, normalization and allocation arithmetic. Application reads authenticated costs and computes a deterministic fingerprint over the normalized settings, source rows/versions, review version, period and calculated comparisons. Save recomputes this basis and rejects stale previews with the existing 409 flow. The saved validated snapshot is independent of later expense edits.

Infrastructure persists nullable JSON snapshots on PricingSimulation and WhatIfSimulation, plus nullable pricing AdditionalDecisionCost. Older rows remain NULL: the website says context was not captured, never that costs were zero. Existing product price/cost snapshots, lifecycle, optimistic concurrency and JWT endpoints remain intact. Preview remains read-only.

The new shared fields are website/Application contracts, not changes to the existing public JSON API request contracts. Legacy disabled-context Application callers remain supported.

## Migration

New migration: **20261019153847_AddOptionalSimulationBusinessCosts**.

It adds only three nullable columns:
- PricingSimulations.CostContext (text JSON)
- PricingSimulations.AdditionalDecisionCost (numeric(18,2))
- WhatIfSimulations.CostContext (text JSON)

The implementation does not run production migrations or start/deploy production.

With the intended LOCAL database configured, build first, then run from the repository root:

```powershell
dotnet build FinSight.slnx
# Development loads the repository's local settings; an existing connection
# environment variable overrides them. Verify it targets your intended LOCAL DB.
$previousEnvironment = $env:DOTNET_ENVIRONMENT
try {
    $env:DOTNET_ENVIRONMENT = 'Development'
    dotnet ef database update 20261019153847_AddOptionalSimulationBusinessCosts --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Migration failed; inspect before continuing.' }
    dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
} finally {
    $env:DOTNET_ENVIRONMENT = $previousEnvironment
}
```

For a later authorized Neon deployment, follow the masked DIRECT-connection process in [DEPLOYMENT.md](DEPLOYMENT.md), verify the branch/database and migration history, apply this migration before releasing the matching application, and keep secrets out of command arguments. No automatic startup migration was added. Do not roll back these columns after saving linked simulations unless their data has been preserved.

## Reference fixture

Monthly eligible costs: rent 1,000,000 + wages 1,500,000 + electricity 500,000 = 3,000,000 IQD.
Product: price 10,000, cost 5,000, current sales 100, proposed price 11,000, expected sales 100.

| Setting | Current contribution | Proposed contribution | Allocation | Current remainder | Proposed remainder | Improvement |
|---|---:|---:|---:|---:|---:|---:|
| Link OFF | 500,000 | 600,000 | none | not calculated | not calculated | 100,000 |
| Link ON, allocation OFF | 500,000 | 600,000 | none | not calculated | not calculated | 100,000 |
| Confirmed 10% allocation | 500,000 | 600,000 | 300,000 | 200,000 | 300,000 | 100,000 |
| Same + 50,000 decision cost | 500,000 | 550,000 | 300,000 | 200,000 | 250,000 | 50,000 |
| Link OFF, keep decision cost | 500,000 | 550,000 | none | not calculated | not calculated | 50,000 |

## Manual checklist

1. In Business Costs enter the fixture month, with inclusion No. Add an inventory purchase and an Unsure expense; neither may inflate the eligible total.
2. Open a product pricing preview. Both switches start OFF. Calculate, close the modal, enable linking and select the month, then calculate context-only.
3. Enable allocation, enter 10%, review the breakdown and confirm the recurring/non-overlap assumption. Verify the table above; repeat with 50,000 decision cost.
4. Close, turn linking OFF, and recalculate. Extra decision cost stays; allocation disappears. The previous result cannot be reopened/applied under changed settings.
5. Save explicitly. Change an expense in another tab before saving another preview: expect 409 and recalculate. A previously saved result must retain its original figures.
6. Test daily/weekly periods, an empty month and delivery with unknown period counts. These remain context-only, not invented zero expenses or per-order overhead deductions.
7. Save/reopen discount and delivery, then edit: linking settings restore, original product financial snapshots remain. Check What If save/history too.
8. Navigate all three cards; cancel/accept a dirty-form prompt. Check desktop/mobile, keyboard, English, Arabic and Kurdish. A different business cannot access the product or saved simulation.

## Verification and limitations

Executed on 2026-10-19:
- `dotnet build --no-restore -v:q`: passed, 0 errors; existing 10 NuGet vulnerability warnings.
- `dotnet test --no-build --no-restore --logger "trx;LogFileName=optional-costs-final.trx"`: **330 unit + 109 integration/browser tests = 439 passed**, 0 failed, 0 skipped.
- PostgreSQL 17 Testcontainers applied migrations to isolated databases; historical schema upgrades and snapshot preservation passed. No development/customer database was migrated by this task.
- `dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build`: no pending model changes (design-time-only dummy local configuration, no database writes).
- New browser cases cover all three cultures at 1440px and 390px: default OFF, independent switches, Arabic-Indic percentage input, allocated results, close/reopen/focus containment/internal scrolling, stale-result invalidation, explicit save, navigation and dirty-form protection.
- An initial browser run caught the active history card suppressing navigation from a saved detail page. Fixed and verified in the complete rerun.
- Existing pricing/discount/delivery/What If, product helper, tenant isolation and lifecycle tests all remain passing.
- After final preview-contract cleanup, rebuild and all 330 unit tests passed again; the focused PostgreSQL/browser rerun passed 12/12 (a subset of the 439 unique tests). Shared-view localization-key audit and git diff whitespace check passed.
- Allocation-result screenshots were inspected in English desktop and Arabic/Badini mobile, alongside navigation/form and modal screenshots.
- Native Badini wording, real-device Safari/Firefox and screen-reader sessions remain manual review items, not claimed as executed.

Supported cultures remain en, ar-IQ and ku-Arab-IQ (Badini/Kurmanji in Arabic script).
New Kurdish financial terminology needs native-speaker review, especially “allocated share,” “contribution” and “estimated remainder.” Arabic text is intended for Iraqi owners.

No whole-business net-profit verdict, product-removal recommendation, daily/weekly proration, verified recurrence, automatic overlap detection or allocation-plan accounting is claimed. Only Chromium automation and screenshot review are covered; physical mobile devices, other browser engines and assistive-technology sessions require manual review. Existing NuGet vulnerability warnings for cryptography packages are outside this focused milestone; they should be addressed separately before release.

## File manifest

- Modified: `src/FinSight.Api/Resources/Website/Localization/RebazText.ar-IQ.resx`
- Modified: `src/FinSight.Api/Resources/Website/Localization/RebazText.ku.resx`
- Modified: `src/FinSight.Api/Resources/Website/Localization/RebazText.resx`
- Modified: `src/FinSight.Api/Views/DeliveryPages/Preview.cshtml`
- Modified: `src/FinSight.Api/Views/DiscountPages/Preview.cshtml`
- Modified: `src/FinSight.Api/Views/SavedSimulationPages/Detail.cshtml`
- Modified: `src/FinSight.Api/Views/SavedSimulationPages/Index.cshtml`
- Modified: `src/FinSight.Api/Views/Shared/_DecisionCards.cshtml`
- Modified: `src/FinSight.Api/Views/Shared/_DecisionResult.cshtml`
- Modified: `src/FinSight.Api/Views/Shared/_Layout.cshtml`
- Removed (replaced by shared linked-context view): `src/FinSight.Api/Views/Shared/_SimulationCostContext.cshtml`
- Modified: `src/FinSight.Api/Views/SimulationPages/Preview.cshtml`
- Modified: `src/FinSight.Api/Views/WhatIfPages/Detail.cshtml`
- Modified: `src/FinSight.Api/Views/WhatIfPages/History.cshtml`
- Modified: `src/FinSight.Api/Views/WhatIfPages/Index.cshtml`
- Modified: `src/FinSight.Api/Views/WhatIfPages/SelectProduct.cshtml`
- Modified: `src/FinSight.Api/Website/Controllers/DeliveryPagesController.cs`
- Modified: `src/FinSight.Api/Website/Controllers/DiscountPagesController.cs`
- Modified: `src/FinSight.Api/Website/Controllers/SimulationPagesController.cs`
- Modified: `src/FinSight.Api/Website/Controllers/WebsiteController.cs`
- Modified: `src/FinSight.Api/Website/Controllers/WhatIfPagesController.cs`
- Modified: `src/FinSight.Api/Website/Models/DeliveryViewModel.cs`
- Modified: `src/FinSight.Api/Website/Models/DiscountViewModel.cs`
- Modified: `src/FinSight.Api/Website/Models/PreviewSimulationViewModel.cs`
- Modified: `src/FinSight.Api/Website/Models/WhatIfViewModels.cs`
- Modified: `src/FinSight.Api/wwwroot/css/site.css`
- Modified: `src/FinSight.Api/wwwroot/js/decision-cards.js`
- Modified: `src/FinSight.Application/DependencyInjection.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Commands/CreateDecisionSimulation/CreateDecisionSimulationCommand.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Commands/CreateDecisionSimulation/CreateDecisionSimulationHandler.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Commands/CreateDecisionSimulation/CreateDecisionSimulationValidator.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Delivery/DeliveryHandlers.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Delivery/DeliveryRequests.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Discounts/DiscountHandlers.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Discounts/DiscountRequests.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/PricingSimulationResponse.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Queries/GetSavedDecision/GetSavedDecisionHandler.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Queries/PreviewPricingDecision/PreviewPricingDecisionHandler.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Queries/PreviewPricingDecision/PreviewPricingDecisionQuery.cs`
- Modified: `src/FinSight.Application/Features/PricingSimulations/Queries/PreviewPricingDecision/PreviewPricingDecisionValidator.cs`
- Modified: `src/FinSight.Application/Features/WhatIf/SaveWhatIfCommand.cs`
- Modified: `src/FinSight.Application/Features/WhatIf/WhatIfResponse.cs`
- Modified: `src/FinSight.Domain/Entities/PricingSimulation.cs`
- Modified: `src/FinSight.Domain/Entities/WhatIfSimulation.cs`
- Modified: `src/FinSight.Domain/Models/PricingDecisionResult.cs`
- Modified: `src/FinSight.Domain/Services/PricingDecisionCalculator.cs`
- Modified: `src/FinSight.Infrastructure/Persistence/Configurations/PricingSimulationConfiguration.cs`
- Modified: `src/FinSight.Infrastructure/Persistence/Configurations/WhatIfSimulationConfiguration.cs`
- Modified: `src/FinSight.Infrastructure/Persistence/Migrations/FinSightDbContextModelSnapshot.cs`
- Modified: `tests/FinSight.IntegrationTests/BrowserJourneyTests.Discounts.cs`
- Modified: `tests/FinSight.IntegrationTests/BusinessCostMigrationTests.cs`
- Modified: `tests/FinSight.IntegrationTests/WebsiteTests.BusinessCosts.cs`
- Modified: `tests/FinSight.IntegrationTests/WebsiteTests.Discounts.cs`
- Created: `docs/OPTIONAL_BUSINESS_COST_LINKING.md`
- Created: `src/FinSight.Api/Views/Shared/_CostLinkForm.cshtml`
- Created: `src/FinSight.Api/Views/Shared/_CostLinkHidden.cshtml`
- Created: `src/FinSight.Api/Views/Shared/_LinkedCostResult.cshtml`
- Created: `src/FinSight.Api/Views/Shared/_SimulationNavigation.cshtml`
- Created: `src/FinSight.Api/Website/Models/CostLinkForm.cs`
- Created: `src/FinSight.Api/wwwroot/js/cost-link.js`
- Created: `src/FinSight.Application/Features/BusinessCosts/SimulationCostContextService.cs`
- Created: `src/FinSight.Domain/Exceptions/CostContextValidationException.cs`
- Created: `src/FinSight.Domain/Models/SimulationCostContext.cs`
- Created: `src/FinSight.Domain/Services/SimulationCostCalculator.cs`
- Created: `src/FinSight.Infrastructure/Persistence/Migrations/20261019153847_AddOptionalSimulationBusinessCosts.Designer.cs`
- Created: `src/FinSight.Infrastructure/Persistence/Migrations/20261019153847_AddOptionalSimulationBusinessCosts.cs`
- Created: `tests/FinSight.IntegrationTests/BrowserJourneyTests.CostLinking.cs`
- Created: `tests/FinSight.IntegrationTests/WebsiteTests.CostLinking.cs`
- Created: `tests/FinSight.UnitTests/Application/Features/BusinessCosts/SimulationCostContextTests.cs`
- Created: `tests/FinSight.UnitTests/Domain/Services/SimulationCostCalculatorTests.cs`
