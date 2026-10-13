# Pricing decision preview and saved history

The website preview uses a pure Domain `PricingDecisionCalculator`. It delegates
contribution calculations to the original `PricingSimulationCalculator`. The
maintenance threshold is the integer ceiling of current contribution divided
by proposed unit contribution, only when both are positive. It excludes fixed
costs and is **not** a business break-even point. A decline scenario rounds
units down; the default 10% is illustrative, never predicted demand.

The website does not recommend a price when expected sales are unknown. In
that case, saved `ExpectedSalesQuantity`, `ProposedContribution`, `Difference`,
and `Recommendation` are null, never zero. The older API create and update
requests still require an integer expected quantity and use the original
calculator. Existing API routes, response fields, types, and enum values are
unchanged. Classic API list/get reads include simulations with known expected
sales; unknown-expected website records stay in the separate website history.
The Application response carries nullable results and the new period, decline,
currency, product-name snapshot, and calculation-version fields.

`AddPricingDecisionAssumptions` makes the four fields nullable and adds
nullable assumption/snapshot columns. Existing rows are preserved with
`CalculationVersion = 0` and no period; the UI displays “Period not specified”
and does not invent a historical decline. New website saves use version 1.
Historical financials are read from each saved simulation, not the current
product. A later formula change must explicitly retain support for versions
0 and 1 before adding a new version.

Apply the migration explicitly to the intended database before using the new
website save/history pages. Do not automatically migrate production on startup.
For local development only:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet ef database update --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --context FinSightDbContext
```

For production, use the existing explicit migration procedure in
`docs/DEPLOYMENT.md` with the direct Neon endpoint, verified TLS, and a
reviewed backup/rollback plan. Do not use the pooled endpoint for migrations.

Before a customer release, have a native Badini/Kurmanji speaker from Duhok
review the new threshold, contribution, and scenario terminology. Also check
Arabic phrasing with an Iraqi owner. Confirm the copy never implies net profit
or a forecast.
