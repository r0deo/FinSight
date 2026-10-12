# Rebaz authentication journey

## Root cause and boundaries

The shared layout previously rendered **Link Google** for every authenticated user whenever Google was enabled. It did not consult external-login records. The existing Google onboarding service already saves the Business, Owner, and Identity login association in one PostgreSQL transaction. Integration tests exercise that persistence and returning sign-in by provider/subject. Production records were not inspected or changed.

Account settings now reads `UserManager.GetLoginsAsync` through Application's `IAccountConnections` abstraction, with the current user's ID and business checked in Infrastructure. It does not infer linking from email or the current session's sign-in method.

The existing Google middleware, stable provider subject, transaction/advisory lock, verified-email checks, roles, explicit linking/XSRF checks, cookie/JWT separation, antiforgery protection, Domain rules and calculation snapshots remain in place. Project/namespace/scheme names and the Data Protection application name remain **FinSight**. Visible website and confirmation-email branding is **Rebaz**.

## Journey and routes

| Route | Behavior |
| --- | --- |
| `/` | Public marketing page; authenticated users go to home. |
| `/home` | Owner/Manager home. Existing tenant-scoped GetProducts query selects the empty/product-present state, including inactive products. |
| `/account/register` | Google option; password form only when Email:Enabled is true. Disabled password registration is rejected server-side as well. |
| `/account/login` | Existing eligible password accounts continue to work when email is disabled. Google uses the existing flow. |
| `/account/google/business` | Pending Google identity completes business setup before any application cookie/tenant access. No password requested. |
| `/account/check-email`, `/account/resend-confirmation`, `/account/confirm-email`, `/account/confirmation-success` | Existing confirmation flow, clearer branding and login destination; disabled resend does not promise delivery. |
| `/account/settings` | Persisted Google connection state. Only an unlinked account is offered Connect Google, when Google is enabled. |
| `/account/google/link` | Legacy GET shows a settings pointer; POST remains the antiforgery-protected, explicitly authenticated OAuth challenge. |
| `/account/google/link-callback` | Existing initiating-user ownership check; success returns to settings. |
| `/account/google/error` | Safe sign-in/settings retry path for cancellation, provider failure or invalid external session. |
| `/products` | Existing product management, plus a per-active-product preview link. |
| `/simulations/preview` | GET without a product directs the user to choose one. GET with productId shows a form; POST runs the existing read-only PreviewPricingSimulationQuery. Nothing is saved or edited. |
| POST `/account/logout` | Existing antiforgery-protected logout; GET is not supported. |

The shared destination policy sends pending business setup to onboarding, completed signup/sign-in to `/home`, and returning users to a safe allowlisted local website page when requested. Authentication/onboarding/callback/API/POST-only paths and ambiguous/external URLs are not return destinations. Signed-in visits to login, registration, or completed onboarding return home. Incomplete Google signup resumes through another Google sign-in without partially creating a tenant or account.

No saved-simulation website existed, so no nonfunctional history navigation was added. The small preview form makes the home action functional without expanding into saved-simulation UI.

## Configuration

No new configuration keys, packages or migrations are required.

- Keep existing real Google credentials and `Authentication__Google__Enabled=true`.
- Keep the trusted origin `Email__WebsiteBaseUrl=https://rebaz.onrender.com`.
- The production Google callback remains `https://rebaz.onrender.com/signin-google`.
- Keep `Email__Enabled=false` until the existing SMTP provider is genuinely configured and tested. Password signup is unavailable on the website and API (400 validation response); confirmed existing password accounts still work. No account is auto-confirmed.
- Re-enable email using the existing settings documented in DEPLOYMENT.md only after delivery works. Google identities without explicitly verified provider email still require confirmation.
- Cookie/JWT secrets and Data Protection setup are unchanged.

## Verification commands

Verified locally on October 12, 2026: build succeeds with zero warnings/errors; 110 unit tests and 31 PostgreSQL integration tests pass (141 total, no failures or skips). EF reports no pending model changes. A controllable-clock integration test verifies external and application cookie expiry without waiting in real time.

Run from the solution root:

```powershell
dotnet build
dotnet test --no-build
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
```

Integration tests use disposable PostgreSQL Docker containers and substitute only Google's network responses. They are not proof of live Google behavior or SMTP delivery. No production database was contacted.

Browser tooling returned no available browsers. A database-disconnected local instance was started for the attempted visual check and then stopped. Desktop/mobile visual review remains manual; no screenshots are claimed.

## Manual live checklist (after an authorized deployment)

1. New Google user: sign in at https://rebaz.onrender.com/account/register, supply business details without a password, land on home, and see Add your first product.
2. Returning Google user: sign out and back in; verify no repeated onboarding and Account settings says Google connected without Connect Google.
3. Abandon onboarding and restart Google sign-in; confirm setup resumes and products/home cannot be accessed before completion.
4. Existing confirmed password user: login, open settings and explicitly connect Google. Sign out and login by password again; connection status must remain connected. Do not auto-link a matching email.
5. With email disabled: no password signup form or usable registration submission; existing confirmed password login remains available.
6. After real email delivery is enabled separately: registration -> check-email -> confirmation -> login -> home. Test an invalid/expired link and rate-limited resend.
7. Create a product, preview its price change, edit/deactivate/reactivate it, and check another business cannot see it. Preview must not save a simulation.
8. Cancel Google consent, retry an expired session, submit an external return URL, and logout. Ensure safe destinations and no authenticated access after logout.
9. Check navigation/forms at phone and desktop widths; confirm no overflow and all actions remain reachable.

## Changed files

- `src/FinSight.Api/Views/Account/CheckEmail.cshtml`
- `src/FinSight.Api/Views/Account/ConfirmEmail.cshtml`
- `src/FinSight.Api/Views/Account/ConfirmationSuccess.cshtml`
- `src/FinSight.Api/Views/Account/Login.cshtml`
- `src/FinSight.Api/Views/Account/Register.cshtml`
- `src/FinSight.Api/Views/Account/ResendConfirmation.cshtml`
- `src/FinSight.Api/Views/GoogleAccount/Business.cshtml`
- `src/FinSight.Api/Views/GoogleAccount/Error.cshtml`
- `src/FinSight.Api/Views/GoogleAccount/ExistingAccount.cshtml`
- `src/FinSight.Api/Views/GoogleAccount/Link.cshtml`
- `src/FinSight.Api/Views/GoogleAccount/Unavailable.cshtml`
- `src/FinSight.Api/Views/Home/Index.cshtml`
- `src/FinSight.Api/Views/ProductPages/Index.cshtml`
- `src/FinSight.Api/Views/Shared/_EmailAvailability.cshtml`
- `src/FinSight.Api/Views/Shared/_GoogleButton.cshtml`
- `src/FinSight.Api/Views/Shared/_Layout.cshtml`
- `src/FinSight.Api/Website/Controllers/AccountController.cs`
- `src/FinSight.Api/Website/Controllers/GoogleAccountController.cs`
- `src/FinSight.Api/Website/Controllers/HomeController.cs`
- `src/FinSight.Api/wwwroot/css/site.css`
- `src/FinSight.Infrastructure/DependencyInjection.cs`
- `src/FinSight.Infrastructure/Identity/AuthenticationService.cs`
- `src/FinSight.Infrastructure/Identity/EmailConfirmationService.cs`
- `src/FinSight.Infrastructure/Identity/GoogleSignInService.cs`
- `tests/FinSight.IntegrationTests/GoogleSignInTests.cs`
- `tests/FinSight.IntegrationTests/WebsiteTests.cs`
- `tests/FinSight.UnitTests/Website/AccountControllerTests.cs`
- `src/FinSight.Api/Views/Account/Settings.cshtml` (new)
- `src/FinSight.Api/Views/Home/Welcome.cshtml` (new)
- `src/FinSight.Api/Views/SimulationPages/Preview.cshtml` (new)
- `src/FinSight.Api/Website/Controllers/SimulationPagesController.cs` (new)
- `src/FinSight.Api/Website/Models/PreviewSimulationViewModel.cs` (new)
- `src/FinSight.Api/Website/PostAuthenticationDestination.cs` (new)
- `src/FinSight.Application/Common/Interfaces/IAccountConnections.cs` (new)
- `src/FinSight.Infrastructure/Identity/AccountConnections.cs` (new)
- `tests/FinSight.UnitTests/Website/PostAuthenticationDestinationTests.cs` (new)
- `docs/AUTHENTICATION-JOURNEY.md` (new)

At the implementation handoff, no commit, push, deployment, production configuration change, or production data change had been performed. Any subsequent release action requires separate authorization.
