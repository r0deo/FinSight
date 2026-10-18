# Calculation result dialogs

## Scope

The existing calculations retain their original handlers, inputs, formulas, warnings,
authorization, antiforgery and persistence rules. Only their result presentation changes:

| Page | Dialog result | Explicit action retained |
| --- | --- | --- |
| `/products/create`, `/products/{id}/edit` | Estimated unit cost, ingredient breakdown, excluded costs and limitations | Use this unit cost; only the separate product Save persists it |
| `/simulations/preview` | Existing pricing decision, assumptions, warnings and optional monthly context | Save this simulation |
| `/what-if/preview` | Existing shock/response comparison, assumptions, warnings and optional monthly context | Save this what-if scenario |
| `/business-costs/adjustment` | Existing conditional saving and revised recorded total | None: this remains a hypothetical, read-only calculation |

Product lists, input forms, saved simulation details/history and the Business Costs
overview remain ordinary pages. Monthly recorded totals/comparisons are not new
popup calculations. No new calculations, endpoints, packages or migrations are added.

## Shared implementation

- `ResultModalTagHelper` renders one copy of the supplied result content and existing
  save/apply forms, a localized title, close control and review-again button.
- `result-modal.js` progressively promotes that section to a native modal `dialog`.
  Without JavaScript/dialog support the same result remains inline and explicit
  form actions still work; there are never two copies of the result.
- Only valid result views open automatically. Product edit GET, Use, Cancel and
  failed requests do not open a result automatically. A saved product breakdown
  can be reviewed intentionally with View calculation results.
- Close/Escape never submits a form or changes its values. Focus goes to the
  calculation button after automatic opening, or the review-again button after
  manual opening. Native modal background inertness is combined with explicit
  Tab/Shift+Tab wrapping. Initial focus goes to the close button, not a saving action.
- Changing the relevant inputs invalidates the review-again button until a fresh
  server calculation. Existing stale-result/save protection is retained.
- Validation remains on the page and focuses its first enabled invalid field,
  opening an enclosing details section when needed. Global errors focus the summary.
  What If's nested `Request.*` validation keys are mapped onto the flat website
  fields for display only; neither rules nor status codes change. The existing
  Business Costs adjustment's combined error is also displayed beside its field.
- Responsive navy/gold styling uses logical properties. Long results scroll inside
  the dialog while its title and close button remain accessible. Existing `en`,
  `ar-IQ` and `ku-Arab-IQ` cultures, fonts and RTL direction are retained. The two new
  Badini labels still need native-speaker wording review.

## Manual verification

For each culture, check at desktop and a narrow mobile viewport:

1. Calculate using valid inputs. Expect one dialog, a localized title, unchanged
   results/warnings, close-button focus and a dimmed, noninteractive page behind it.
2. Tab and Shift+Tab through controls; focus must stay inside. Scroll a long result;
   its close button must remain reachable. Press Escape and check focus returns to
   Calculate and input values are unchanged. Review again, then use the close button.
3. Edit an assumption. The old result must not be reopenable. Recalculate and confirm
   the new result, not the old one. No save/apply occurs just by opening/closing.
4. Product reference: 1 kg pack at 12000 IQD, 1 kg used, 10 servings, 200 packaging,
   explicit 0 other costs gives 1400. Change packaging to 300: expect 1500. Use fills
   UnitCost, but only Add product/Save changes persists it.
5. Pricing/What If: explicitly Save from the dialog, then verify the normal saved
   detail page. Business Costs: 150 recorded rent with a 10% assumption gives 15
   conditional saving; 20% gives 30. Recorded rent must remain 150.
6. Submit invalid input on each screen. No dialog should open; expect a readable
   field error and focus on the invalid control, with the other inputs retained.

## Verification coverage

The existing real-HTTPS Chromium journeys now exercise all four flows at 1440px and
390px in all three cultures. Shared assertions cover title association, focus trap,
Escape/close/reopen, unchanged form values and viewport bounds. Database assertions
check explicit saving, no preview writes, retained product costs and historical
snapshots. Shared component unit tests cover HTML encoding, localized controls,
non-submitting close/reopen buttons and preservation of the original result forms.

Screenshots are ignored under `tests/FinSight.IntegrationTests/bin/Debug/net10.0/TestResults/browser`.
These tests use disposable PostgreSQL, not Neon/production. Live Render, physical
mobile devices, non-Chromium engines and assistive-technology testing are separate
manual checks; automated keyboard/ARIA checks do not claim a screen-reader audit.

## Executed results — 2026-10-17

- `dotnet build --no-restore -v quiet`: passed, 0 errors. Ten existing NU1902/NU1903
  advisory warnings remain for the cryptography dependencies; no packages changed.
- `dotnet test --no-build --no-restore --logger "console;verbosity=minimal"`:
  **318 passed, 0 failed, 0 skipped** (242 unit + 76 PostgreSQL/integration/browser).
- All 12 updated browser cases passed: product-cost journeys plus combined pricing,
  What If and Business Costs journeys in three languages at two viewport widths.
  They include invalid-field focus, Tab/Shift+Tab, Escape, close/reopen, preserved
  form values, internal scrolling, recalculation and explicit save/apply checks.
- Representative screenshots of all four dialog types were visually inspected,
  covering desktop/mobile, English, Arabic and Badini RTL. The cost example displays
  1400 IQD; changing packaging to 300 displays 1500 without saving until requested.
- Docker was initially unavailable. Starting Docker Desktop resolved this; the final
  full integration run used disposable PostgreSQL containers successfully. Early
  browser failures exposed focus leaving the dialog and What If field-error key
  mismatches; both were fixed before the passing full run.
- `git diff --check` passed (Git notes normal CRLF-to-LF normalization for CSS).
- No local requested check remains blocked. Live deployment, screen readers,
  physical devices and other browser engines were not tested. Native Badini wording
  review remains a manual follow-up.
- No production data, migrations, commits, pushes or deployments were performed.

## Changed-file list

- `src/FinSight.Api/Website/ResultModalTagHelper.cs`
- `src/FinSight.Api/wwwroot/js/result-modal.js`
- `src/FinSight.Api/wwwroot/css/site.css`
- `src/FinSight.Api/Views/Shared/_Layout.cshtml`
- `src/FinSight.Api/Views/ProductPages/_CostHelper.cshtml`
- `src/FinSight.Api/Views/SimulationPages/Preview.cshtml`
- `src/FinSight.Api/Views/WhatIfPages/Index.cshtml`
- `src/FinSight.Api/Views/BusinessCostPages/Index.cshtml`
- `src/FinSight.Api/Views/BusinessCostPages/_AdjustmentForm.cshtml`
- `src/FinSight.Api/Website/Controllers/WhatIfPagesController.cs` (website error-field mapping only)
- `src/FinSight.Api/Resources/Website/Localization/RebazText.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ar-IQ.resx`
- `src/FinSight.Api/Resources/Website/Localization/RebazText.ku.resx`
- `tests/FinSight.UnitTests/Website/ResultModalTests.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.ResultModals.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.ProductCostHelper.cs`
- `tests/FinSight.IntegrationTests/BrowserJourneyTests.cs`
- `docs/CALCULATION_RESULT_MODALS.md`
