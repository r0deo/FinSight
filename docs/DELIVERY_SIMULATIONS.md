# Financial decisions and delivery simulation

## Scope and screens

- General heading: **Simulate your financial decisions** / **جرّب قراراتك المالية**. Navigation stays **Simulations** / **المحاكاة**.
- Shared decision cards on price preview, discount preview, delivery preview, product selection, and saved history. Three equal desktop columns, two tablet columns, one mobile column; native links, visible focus, checkmark and current-page semantics.
- Existing routes remain valid. The navigation/home entry opens `/simulations/preview`; without a product, it now offers a tenant-filtered active-product picker instead of redirecting away. History remains `/simulations`.
- `/simulations/delivery?productId=<id>`: GET inputs; POST `/simulations/delivery`: preview only.
- POST `/simulations/delivery/save`: explicit create, then redirect to existing `/simulations/<id>` history detail.
- GET/POST `/simulations/<id>/delivery/edit`: load/edit-preview. POST `/simulations/<id>/delivery/save`: explicit versioned update.
- Price change, discount, and delivery each render only their own form. What If and saved history remain secondary links, not extra cards/nav items.
- Shared existing result modal retains title, Escape/close, focus management, mobile scrolling, reopen and input-change invalidation.
- Card switching preserves the product and asks before discarding changed inputs or a POST preview. It never submits or saves. Drafts are not stored. This enhancement requires JavaScript; ordinary links and server forms remain functional without JavaScript.

## Calculation and boundaries

Domain `DeliverySimulationCalculator` owns validation and decimal calculation, reusing `PricingSimulationCalculator` for direct contribution. Input money is nonnegative with at most two decimals and bounded to `numeric(18,2)`; delivery price must be positive. Commission is explicitly supplied, 0–100%, at most four decimals. Order counts are whole numbers. At least one unit/order is required.

- Direct/order = units × (saved direct price − saved unit cost).
- Delivery subtotal = units × delivery price.
- Commission/order = subtotal × commission %, rounded to two decimals **away from zero**.
- Delivery/order = subtotal − rounded commission − units × cost − extra packaging/order − other charges/order.
- Complete-period baseline = Q × direct/order.
- Complete-period scenario = (Q − M) × direct/order + N × delivery/order − included fixed cost.
- Difference = scenario − baseline. Transferred orders must be <= both known Q and N. Remaining direct orders are not charged platform fees.
- An incomplete Q/N/M combination returns only per-order figures, never an assumed zero transfer count or overall recommendation.
- Blank fixed cost remains null/unknown/excluded, unlike explicitly entered zero. Period arithmetic excludes null fixed cost while the UI keeps the exclusion visible.
- One product and the same units/order for both channels. Direct demand is otherwise unchanged. No mixed basket, demand/capacity prediction, real orders, provider integration, or net-profit claim.
- Existing overhead and costs already included in unit cost are not deducted again. Taxes, refunds, promotions and unentered fees remain excluded.

The 6,000 / 3,000 IQD reference with 25% commission, 500 packaging, zero other charges, 20,000 fixed costs, Q=100 and N=50 yields delivery/order=1,000:
M=10 -> scenario 300,000, difference 0; M=0 -> 330,000 / +30,000; M=50 -> 180,000 / −120,000.

## Architecture and security

No new packages, tables, provider integrations, or authentication schemes. Application CQRS handlers use existing product/simulation repositories, currency reader, authenticated user context and TimeProvider. Website validation uses FluentValidation plus nullable required model-binding fields; charges never default silently to zero.

Identity, business, product prices/costs and currency come from server-side authenticated access. Active tenant products only for new previews/saves; missing/cross-tenant products and simulations return 404. Owner and Manager are allowed. Existing cookie policy, antiforgery, exception handling and JWT contracts remain unchanged.

A new immutable `DeliverySnapshot` stores input null status, prices/costs, results, calculation version, commission basis and unchanged-direct-demand assumption. Snapshot presence discriminates this type, following the existing discount approach. Currency, product name, period, lifecycle and version remain on the existing aggregate. Reopening does not recompute. Editing recalculates using the ORIGINAL financial snapshot, not today's product.

Legacy required columns are compatibility projections, not authoritative delivery data: current quantity/contribution use zero when unavailable because those old columns are non-nullable. The delivery UI and snapshot retain and display null correctly. Classic API read/list explicitly exclude delivery and discount snapshots to avoid presenting order-based figures as classic unit-sales results. Existing JWT lifecycle endpoints can archive/delete/restore delivery records; classic update rejects a delivery record with 409. Atomic EF version checks and existing restoration rules are retained. No new lifecycle UI was added.

## Migration and rollout

Required migration: **20261019084302_AddDeliverySimulationSnapshot**. Adds only nullable text `PricingSimulations.DeliverySnapshot`. Existing records stay null and no historical calculation is rewritten.

Verified against disposable PostgreSQL 17 databases, including upgrade from the immediately preceding discount schema and from the older business-cost schema. No production database or persistent local development database was migrated in this task.

Before serving this release, an authorized operator must apply the migration to the intended database. Configure its connection securely as described in [DEPLOYMENT.md](DEPLOYMENT.md); do not put credentials in commands or source. From the solution root, after confirming the configured target:

```powershell
dotnet build
dotnet ef database update 20261019084302_AddDeliverySimulationSnapshot --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
dotnet ef migrations list --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
```

For Neon, follow the existing masked direct-URL migration helper workflow in DEPLOYMENT.md; this task did not run it. Startup does not migrate. Back up according to the deployment procedure; rolling back this column after delivery saves would discard those snapshots.

## Verification

Commands used:
```powershell
dotnet build --no-restore
dotnet test --no-build --no-restore --logger "trx;LogFileName=financial-decisions.trx"
dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
```

The model-check command used a process-local dummy design-time connection, not production. PostgreSQL/HTTP/browser tests provision isolated containers and test accounts; no customer data or production credentials are used.

Final verification on 2026-10-19: build succeeded; **315 unit tests + 97 integration/browser tests = 412 passed, zero failed/skipped**. The full suite includes 4 delivery PostgreSQL website cases (all three languages), 6 delivery Chromium Manager journeys (en, ar-IQ, ku-Arab-IQ at 1440 and 390 px), old price/discount flows, and schema-upgrade tests. EF reported no pending model changes.

Browser assertions cover card keyboard focus and product retention, dirty-switch cancel/accept, validation focus, Arabic-Indic input, modal focus/scroll/close/reopen, fresh results, preview without persistence, explicit save, and original snapshot after product edits. Screenshots inspected for English desktop, Arabic mobile/desktop, and Kurdish mobile. Existing price/discount journeys run in the full suite. Automated checks are not a substitute for assistive-technology or native-language review.

Existing NU1902/NU1903 warnings for transitive System.Security.Cryptography.Pkcs/Xml 10.0.12 remain; dependency upgrades were not mixed into this milestone.

## Manual check

1. Log in as Owner or Manager. Create/choose an active product with price 6,000 and cost 3,000 IQD. Open Simulations.
2. Confirm all three cards and product picker. Choose the product, switch cards; change a field and verify cancel keeps inputs, confirm switches without saving.
3. Choose delivery. Enter units=1, price=6000, commission=25, packaging=500, other=0, fixed=20000; period=month. Expand comparison: Q=100, N=50, M=10.
4. Calculate: modal shows direct/order=3000, commission=1500, delivery/order=1000, scenario=300000, difference=0.
5. Close, change M to0 then50; recalculate and check 330000/+30000 then180000/−120000. Old results must not remain available after changing inputs.
6. Leave M blank: per-order results only. Leave fixed cost blank: visible exclusion; explicit0 stays distinct. Try commission101 or M>N: readable field error.
7. Save explicitly, reopen from history, change the PRODUCT's financials, reopen and edit the simulation: saved original price/cost/results remain unchanged unless explicitly editing its assumptions. A second update with the old version returns409.
8. A different business must receive404 for the product/simulation/edit URLs and never see the history entry.
9. Repeat in Arabic and Badini Kurdish on a narrow screen. Check card order, focus, labels, scrolling, modal close and retained inputs.

## Translation review and limitations

English `en`, Iraqi Arabic `ar-IQ`, Badini/Kurmanji Arabic script `ku-Arab-IQ` with existing `ku` resources. New entries are complete in all sets. No Sorani culture substitution. **Badini wording requires a Duhok native-speaker review**, particularly commission, contribution/amount remaining, transferred orders, and fixed/variable-cost terminology. Contribution is deliberately not translated as net profit.

No live delivery-platform or production tests were performed (none are needed for this manual model). Chromium was exercised; Safari/Firefox, screen readers and formal translation review remain unverified.

## Changed files

The prior discount-only picker was removed in favor of the shared picker; it remains recoverable through Git. No commits, pushes, deployments, production migrations, or new secrets.

- `src/FinSight.Api/Resources/Website/Localization/RebazText.ar-IQ.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ku.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.resx`
- `src/FinSight.Api/Views/DeliveryPages/Preview.cshtml`
- `src/FinSight.Api/Views/DiscountPages/ChooseProduct.cshtml` (removed; replaced by shared picker)
- `src/FinSight.Api/Views/DiscountPages/Preview.cshtml`
- `src/FinSight.Api/Views/Home/Welcome.cshtml`
- `src/FinSight.Api/Views/SavedSimulationPages/Detail.cshtml`
- `src/FinSight.Api/Views/SavedSimulationPages/Index.cshtml`
- `src/FinSight.Api/Views/Shared/_DecisionCards.cshtml`
- `src/FinSight.Api/Views/Shared/_DeliveryResult.cshtml`
- `src/FinSight.Api/Views/Shared/_Layout.cshtml`
- `src/FinSight.Api/Views/Shared/DecisionProductPicker.cshtml`
- `src/FinSight.Api/Views/SimulationPages/Preview.cshtml`
- `src/FinSight.Api/Website/Controllers/CultureController.cs`
- `src/FinSight.Api/Website/Controllers/DeliveryPagesController.cs`
- `src/FinSight.Api/Website/Controllers/DiscountPagesController.cs`
- `src/FinSight.Api/Website/Controllers/SimulationPagesController.cs`
- `src/FinSight.Api/Website/Controllers/WebsiteController.cs`
- `src/FinSight.Api/Website/Models/DecisionCardsViewModel.cs`
- `src/FinSight.Api/Website/Models/DeliveryViewModel.cs`
- `src/FinSight.Api/wwwroot/css/site.css`
- `src/FinSight.Api/wwwroot/js/decision-cards.js`
- `src/FinSight.Application/DependencyInjection.cs`
- `src/FinSight.Application/Features/PricingSimulations/Delivery/DeliveryHandlers.cs`
- `src/FinSight.Application/Features/PricingSimulations/Delivery/DeliveryRequests.cs`
- `src/FinSight.Application/Features/PricingSimulations/PricingSimulationResponse.cs`
- `src/FinSight.Application/Features/PricingSimulations/Queries/GetSavedDecision/GetSavedDecisionHandler.cs`
- `src/FinSight.Domain/Entities/PricingSimulation.cs`
- `src/FinSight.Domain/Exceptions/DeliveryValidationException.cs`
- `src/FinSight.Domain/Models/DeliverySimulationSnapshot.cs`
- `src/FinSight.Domain/Services/DeliverySimulationCalculator.cs`
- `src/FinSight.Infrastructure/Persistence/Configurations/PricingSimulationConfiguration.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/20261019084302_AddDeliverySimulationSnapshot.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/20261019084302_AddDeliverySimulationSnapshot.Designer.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/FinSightDbContextModelSnapshot.cs`
- `src/FinSight.Infrastructure/Persistence/Repositories/PricingSimulationRepository.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.Delivery.cs`
- `tests/FinSight.IntegrationTests/BusinessCostMigrationTests.cs`
- `tests/FinSight.IntegrationTests/DeliveryMigrationTests.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.Delivery.cs`
- `tests/FinSight.UnitTests/Application/Features/PricingSimulations/DeliveryHandlerTests.cs`
- `tests/FinSight.UnitTests/Domain/Services/DeliverySimulationCalculatorTests.cs`
- `tests/FinSight.UnitTests/Website/LocalizationTests.cs`
- `docs/DELIVERY_SIMULATIONS.md` (this handoff)
