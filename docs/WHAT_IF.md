# What If? (first cost-and-demand shock)

The authenticated website provides `/what-if` (active product selection),
`POST /what-if/preview`, `POST /what-if/save`, `/what-if/history`, and
`/what-if/{id}`. Owner and Manager use the existing website cookie and
antiforgery protection. Business and user IDs come only from authenticated
claims; product and saved-scenario lookups include the business ID. Preview
does not write. Saving requires a recent Data Protection proof for the exact
server-calculated inputs and financial snapshot, followed by another active
product and price/cost check.

`WhatIfSimulations` is a separate table; legacy pricing simulations and their
API contracts are unchanged. Each row has a product/user/business reference,
product and currency snapshot, period, immutable input and calculated-result
JSON snapshots, calculation version, UTC timestamps, and a version value.
Reopening reads the saved result, not today's Product values. The first version
is append-only; there is no editing or physical deletion of these scenarios.

Amounts are bounded to 0–999,999,999.99 and two decimal places; quantities to
0–1,000,000; cost-increase percentages to 0–10,000 and two places; decline
percentages to 0–100 and two places. These bounds keep every calculated amount
within PostgreSQL `numeric(18,2)`. A percentage shock rounds the resulting unit
cost once to two decimals, midpoint away from zero. Sales decline rounds down
to whole units. All contributions reuse `PricingSimulationCalculator`, and
the sales-maintenance threshold reuses the existing pricing-decision rule.
The threshold is unavailable when the original contribution is nonpositive,
the option's unit contribution is nonpositive, or the required quantity exceeds
the supported range. This is not net profit or business break-even.

Migration: `20261013120101_AddWhatIfSimulations`. Apply it explicitly to the
intended database before serving this feature. For local Docker PostgreSQL,
set `DOTNET_ENVIRONMENT=Development` and run:

```powershell
dotnet ef database update --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --context FinSightDbContext
```

For Neon, use the approved direct-URL secure migration procedure in
`docs/DEPLOYMENT.md`; do not put credentials on a command line or apply this
migration to production without the release decision. The integration suite
applies migrations to its disposable PostgreSQL container.

Arabic and Badini/Kurmanji (`ku-Arab-IQ`) strings are initial translations;
have a Duhok native speaker review the financial vocabulary before a broad
customer release.
