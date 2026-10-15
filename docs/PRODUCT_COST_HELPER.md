# Product-cost helper

## Scope and workflow

The existing `/products/create` and `/products/{id}/edit` pages now have an optional
**Help me calculate this cost** section. Manual UnitCost entry remains available.
The form uses the authenticated business currency; customer ingredient names are
never translated. Owner and Manager website authorization is unchanged.

1. Enter ingredients for one batch, usable output, and optional per-serving costs.
2. **Calculate estimated cost** reviews a server-calculated breakdown. It writes nothing.
3. **Use this unit cost** fills the product form. It also writes nothing.
4. Only **Add product / Save changes** saves the selected cost and breakdown together.
5. Cancel calculation discards the draft and restores the prior cost; on edit it
   restores the existing saved breakdown when appropriate. Leaving without saving
   cannot change the product. Reopen Edit to see a saved breakdown.

A manual cost change clears the old breakdown in the same write, so the website
never shows an old recipe as the explanation for a different cost. The explicit
**Keep manual unit cost** action also allows clearing a breakdown at the same cost.
Existing JSON API request/response contracts remain manual-cost contracts and are
unchanged. An API price-only edit with an unchanged cost retains the breakdown;
an API change of UnitCost clears it. No simulation formulas or snapshots changed.

## Calculation and limits

The pure Domain `ProductCostCalculator` owns all arithmetic. Preview and Save use
the same calculator; JavaScript only manages the form, not financial arithmetic.

- Ingredient cost = pack price × compatible-unit quantity used ÷ pack quantity.
- Unit cost = sum of ingredient costs ÷ usable output + entered packaging + entered
  other variable costs per serving.
- Kilograms/grams and litres/millilitres convert by 1,000; items only match items.
  Mass never converts to volume. Fractional quantities (including part of an item)
  are allowed. Using more than one pack is allowed.
- 1–30 ingredients; trimmed ingredient names of 1–100 characters.
- Quantities and output: greater than zero, at most 1,000,000, at most six decimals.
- Entered prices/optional costs: 0–999,999,999,999.99, at most two decimals.
- Keep decimal precision through ingredient and per-serving calculations; round
  only the final selected UnitCost to two decimals, midpoint away from zero.
  Displayed breakdown amounts are also rounded to two decimals, so displayed lines
  need not add exactly when fractions of a cent exist. The persisted breakdown
  retains full calculation precision and a calculation version.
- Reject results beyond PostgreSQL numeric(18,2), instead of leaking overflow or
  rounding an out-of-range result into the database.
- Required blank fields are errors, not zero. Optional blank costs remain null and
  are explicitly labelled excluded; explicit zero remains distinguishable.
- Usable output already excludes discarded items. Do not deduct discards again.
  Monthly rent/salaries remain in Business Costs, not automatically allocated here.
  This estimate is not net profit or a complete business-cost calculation.

Reference: 12,000 IQD ingredients / 10 servings + 200 packaging + explicit 0 other
costs = **1,400 IQD**. A 1 kg pack costing 10,000 IQD, using 200 g, costs **2,000 IQD**.

## Architecture, security and concurrency

- Domain: ingredient units, immutable calculation records, bounded decimal
  calculation, and controlled Product methods for applying/clearing a breakdown.
- Application: read-only `PreviewProductCostQuery`, FluentValidation, optional
  inputs on existing create/update commands, and selected-cost consistency checks.
  Existing repository abstractions and authenticated business context are reused.
- Infrastructure: nullable JSON serialized into a text column on Products. One
  SaveChanges operation persists both the cost and the breakdown, with no new
  repository or separate recipe system.
- Website: existing cookie policy, antiforgery, tenant-safe edit lookups, localized
  numeric binder, Razor form and minimal JavaScript. Helper actions on another
  business's product return 404 before calculation. No submitted BusinessId is used.
- UnitCost and CostBreakdown are EF concurrency tokens. Their original values are
  included in the UPDATE predicate; simultaneous tracked writes cannot leave a new
  breakdown paired with a different manual cost. Conflicts use the existing 409
  handling. This is a database read-to-write guard, **not** a new browser form
  version/ETag scheme: a later fresh request can intentionally replace an earlier
  save. Existing simulation/month concurrency mechanisms remain unchanged.
- Review fingerprints only detect a changed draft between Calculate and Use for
  UX. They are not authorization proofs; inputs are always validated/recalculated
  on the server, including Save. No client-calculated results are trusted.

## Migration and deployment

New migration: `20261015141948_AddProductCostBreakdown` adds nullable text
`Products.CostBreakdown`. Existing rows stay null, with no invented breakdowns or
changes to their UnitCost. No customer data is seeded. Startup does not migrate.

Before releasing this code, back up/verify the target database and explicitly apply
the migration. Old code remains compatible with the added nullable column. Do not
roll it back after people have saved breakdowns: that would discard their recipes.

Local development only (verify your environment points to your local database):

```powershell
cd D:\FinSight
$env:DOTNET_ENVIRONMENT = 'Development'
try {
    dotnet ef database update --project src/FinSight.Infrastructure --startup-project src/FinSight.Api
    if ($LASTEXITCODE -ne 0) { throw 'Migration failed.' }
    dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api
} finally {
    Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue
}
```

For Neon, follow the masked direct-connection instructions in `DEPLOYMENT.md`:

```powershell
dotnet build tools/FinSight.NeonMigrate/FinSight.NeonMigrate.csproj
if ($LASTEXITCODE -eq 0) {
    dotnet tools/FinSight.NeonMigrate/bin/Debug/net10.0/FinSight.NeonMigrate.dll
}
```

Enter the direct Neon URL only at its masked prompt, verify the target, and confirm
APPLY. Check the verified history includes `20261015141948_AddProductCostBreakdown`.
These are operator instructions, **not a production migration performed for this
milestone**. Tests apply migrations only to disposable local PostgreSQL databases.

## Languages and manual checks

Supported existing cultures: `en`, `ar-IQ`, `ku-Arab-IQ` (Badini/Kurmanji Arabic
script, `.ku.resx` fallback). New helper labels/errors are in all three resources.
Kurdish wording is an initial translation and still needs native Badini-speaker
review, especially ingredient, usable output, variable cost and estimated unit
cost. It is not intended as Sorani. The logo/theme are unchanged; logical CSS and
isolated currency/amount text retain RTL readability.

1. Sign in as Owner or Manager. Create a product manually; verify it has no recipe.
2. Open helper and enter the 12,000 / 10 + 200 + 0 reference. Calculate, then Use.
   Reload without Save: the product must be unchanged (or not created).
3. Repeat and Save; reopen Edit and check all ingredients, units and optional costs.
4. Change ingredients, Calculate/Use, then Cancel: check the original cost/recipe.
5. Save a manual cost; the old recipe must disappear. Try missing/negative amounts,
   zero output and kg-to-litres: useful validation, no write.
6. Try Arabic/Persian digits and Arabic decimal separator in Arabic/Kurdish. Check
   keyboard input, add/remove rows, narrow mobile and RTL display. Ambiguous grouping
   separators are rejected by the existing website number parser.
7. Preview a new simulation after saving: it uses the new UnitCost. Reopen an old
   pricing/What If simulation: its historical snapshot is unchanged. Another business
   must receive 404 for this product's edit/helper actions.

## Verification commands

```powershell
dotnet build
dotnet test
dotnet test tests/FinSight.IntegrationTests --filter "FullyQualifiedName~ProductCostHelper"
```

Integration tests require Docker and create disposable PostgreSQL 17 containers.
Browser tests use real HTTPS Kestrel and Playwright Chromium at 1440px and 390px for
all three cultures. Screenshots are ignored under integration-test `bin/.../TestResults/browser`.
No provider credentials or production database are used.

### Executed results — 2026-10-15

- `dotnet build --no-restore`: passed, 0 errors, 10 existing NuGet advisory warnings.
- `dotnet test --no-build --no-restore`: **301 passed, 0 failed, 0 skipped**:
  233 unit tests and 68 PostgreSQL/integration/browser tests.
- `dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build`:
  no pending model changes (Development design-time context).
- PostgreSQL 17 disposable databases: fresh migrations and upgrade from legacy
  schema passed, preserving manual product costs and both simulation types.
- Eight helper HTTP/PostgreSQL cases cover calculate/use/cancel/save/reopen,
  missing inputs, locale digits, add/remove row state, tenant access, antiforgery,
  mismatched submitted cost, both historical simulation types and competing
  manual/helper database writes in either order.
- Six new Chromium helper journeys passed: `en`, `ar-IQ`, `ku-Arab-IQ`, each at
  1440px desktop and 390px mobile. Screenshots for all six combinations were
  visually inspected; automated checks also verify direction, no horizontal
  overflow, no browser script/console errors, saved data and cancellation.
  Existing product/simulation/Business Costs browser journeys also passed.
- Early verification caught and fixed a real row-removal bug (ModelState entries
  were cleared before their raw values/errors were copied). Test-only issues were
  also fixed: a helper-name overload collision, the shared fixture's login quota,
  a culture-specific number assertion and a login page-load timing wait. The final
  complete run above passed after those corrections.
- No requested local check was blocked. Live Render, production database and
  non-Chromium browsers were not tested. The developer's normal local database and
  Neon were **not migrated**; only disposable test databases received this migration.

No packages were added. Builds still report existing NU1902/NU1903 security
advisories for `System.Security.Cryptography.Pkcs` and `System.Security.Cryptography.Xml`
10.0.12. Those dependency updates are a separate security follow-up, not silently
changed in this feature. Native Badini translation review is also still pending.
No production migration, deployment, Git commit or push is part of this milestone.

## Changed-file manifest

- `docs/DEPLOYMENT.md`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ar-IQ.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ku.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.resx`
- `src/FinSight.Api/Views/ProductPages/Form.cshtml`
- `src/FinSight.Api/Website/Controllers/ProductPagesController.cs`
- `src/FinSight.Api/Website/Models/ProductModels.cs`
- `src/FinSight.Api/wwwroot/css/site.css`
- `src/FinSight.Application/Features/Products/Commands/CreateProduct/CreateProductCommand.cs`
- `src/FinSight.Application/Features/Products/Commands/CreateProduct/CreateProductHandler.cs`
- `src/FinSight.Application/Features/Products/Commands/CreateProduct/CreateProductValidator.cs`
- `src/FinSight.Application/Features/Products/Commands/UpdateProduct/UpdateProductCommand.cs`
- `src/FinSight.Application/Features/Products/Commands/UpdateProduct/UpdateProductHandler.cs`
- `src/FinSight.Application/Features/Products/Commands/UpdateProduct/UpdateProductValidator.cs`
- `src/FinSight.Application/Features/Products/ProductResponse.cs`
- `src/FinSight.Domain/Entities/Product.cs`
- `src/FinSight.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/FinSightDbContextModelSnapshot.cs`
- `src/FinSight.Infrastructure/Persistence/Repositories/ProductRepository.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.cs`
- `tests/FinSight.IntegrationTests/BusinessCostMigrationTests.cs`
- `tests/FinSight.IntegrationTests/Infrastructure/FinSightApiFactory.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.BusinessCostSchema.cs`
- `docs/PRODUCT_COST_HELPER.md`
- `src/FinSight.Api/Views/ProductPages/_CostHelper.cshtml`
- `src/FinSight.Api/Website/Controllers/ProductPagesController.CostHelper.cs`
- `src/FinSight.Api/Website/Models/ProductCostModels.cs`
- `src/FinSight.Api/wwwroot/js/product-cost-helper.js`
- `src/FinSight.Application/Features/Products/ProductCostRequests.cs`
- `src/FinSight.Domain/Enums/IngredientUnit.cs`
- `src/FinSight.Domain/Exceptions/ProductCostValidationException.cs`
- `src/FinSight.Domain/Models/ProductCostBreakdown.cs`
- `src/FinSight.Domain/Services/ProductCostCalculator.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/20261015141948_AddProductCostBreakdown.Designer.cs`
- `src/FinSight.Infrastructure/Persistence/Migrations/20261015141948_AddProductCostBreakdown.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.ProductCostHelper.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.ProductCostHelper.cs`
- `tests/FinSight.UnitTests/Application/Features/Products/ProductCostHandlerTests.cs`
- `tests/FinSight.UnitTests/Domain/Services/ProductCostCalculatorTests.cs`
