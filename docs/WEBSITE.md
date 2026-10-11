# FinSight website foundation

For the subsequent email-confirmation/Google milestone, production configuration,
current test results and changed files, see [AUTHENTICATION-SETUP.md](AUTHENTICATION-SETUP.md).

The existing API host now serves MVC/Razor pages. Product operations use the existing
Application requests through MediatR; MVC never calls the API over HTTP. No database
model changes or new packages are required for this milestone.

## Run locally (PowerShell, from D:\FinSight)

```powershell
docker compose up -d
dotnet build FinSight.slnx --configuration Release
dotnet ef database update --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --configuration Release --no-build
dotnet run --project src/FinSight.Api --configuration Release --no-build --launch-profile https --urls https://localhost:7080
```

Use HTTPS: authentication and antiforgery cookies are always Secure and HttpOnly.
On a new development machine, trust the development certificate:

```powershell
dotnet dev-certs https --trust
```

The existing JWT signing key must remain in user secrets or environment variables.
If it has not been configured on a new machine, generate one (do not replace an
existing key merely to run the website):

```powershell
$finsightKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
dotnet user-secrets set "Jwt:SigningKey" "$finsightKey" --project src/FinSight.Api
Remove-Variable finsightKey
```

Development configuration uses the PostgreSQL and Mailpit services in
`docker-compose.yml`. `Email:WebsiteBaseUrl` is `https://localhost:7080` and is used
to build email links from configuration, never from a client-supplied Host header.
If you change the HTTPS port, change this setting too.

## Pages and flow

| URL | Purpose |
| --- | --- |
| https://localhost:7080/ | Public introduction; signed-in users go to products |
| https://localhost:7080/account/register | Business and first Owner registration |
| https://localhost:7080/account/check-email | Confirmation instructions and next step |
| https://localhost:7080/account/login | Existing Identity account login |
| https://localhost:7080/products | Active products, pagination, page size |
| https://localhost:7080/products?includeInactive=true | Active and inactive products |
| https://localhost:7080/products/create | Add product |
| https://localhost:7080/products/{id}/edit | Edit an existing product |
| http://localhost:8025 | Mailpit inbox for local confirmation messages |
| https://localhost:7080/openapi/v1.json | Existing API OpenAPI document |

Register, open the confirmation link in Mailpit, press **Confirm email & continue**,
then log in. The owner is directed to `/products/create`. Confirmation happens on
an antiforgery-protected POST, so email scanners opening the GET link do not mutate
the account. Unconfirmed accounts cannot sign in.

Create/edit use POST forms at their page URLs. Deactivate and reactivate use POST
`/products/{id}/deactivate` and `/products/{id}/reactivate`. Logout uses POST
`/account/logout`. All forms carry antiforgery tokens. Actions show success messages
and redirect after success; invalid forms render field/summary errors with HTTP 400.
Deactivated products remain stored. No website action physically deletes a product.

## Authentication and tenant boundaries

- `Identity.Application` is the website cookie scheme. Cookies expire after eight
  hours with sliding renewal; they are nonpersistent, Secure, HttpOnly, SameSite=Lax.
  Identity's security-stamp validator revalidates sessions. Password sign-in checks
  email confirmation and applies Identity lockout on failures.
- API authorization explicitly uses `Bearer`. A cookie alone cannot authenticate
  an API call, and a JWT alone cannot authenticate a protected website page.
- The default policy scheme selects Bearer for `/api` and `/openapi`, and the cookie
  scheme for pages. The `Website` authorization policy explicitly selects the cookie.
- Website authentication failures redirect to login; unauthorized roles go to the
  access-denied page. API failures keep JSON ProblemDetails with 401/403 and no HTML
  redirects. Owner and Manager roles can manage products.
- A claims principal factory obtains user ID, business ID and roles from Identity.
  Existing `ICurrentUserContext` reads those claims. Product forms expose no BusinessId;
  extra posted fields cannot change the tenant. Existing handlers/repositories still
  look up products by both product ID and authenticated business ID.
- Return URLs are restricted to local URLs. The layout contains only working links.
  No JWT or refresh token is created by website login or stored in the browser.

## Verification

```powershell
dotnet build FinSight.slnx --configuration Release
dotnet test FinSight.slnx --configuration Release --no-build
docker compose ps
```

Unit tests cover login validation and local-return-URL enforcement. PostgreSQL
integration tests use disposable Testcontainers databases and test registration,
confirmation, cookie login/logout, create/edit/deactivate/reactivate, pagination,
validation, antiforgery enforcement, Owner/Manager roles, JWT compatibility,
unauthorized roles, and cross-business read/write isolation.

Docker is required for integration tests. The verification run for this milestone
passed 57 unit tests and 16 integration tests. The local HTTPS host also started.
Desktop/mobile visual browser checks remain manual: the available browser connector
had no connected browser. Check widths around 390px and 1440px, keyboard focus, form
errors and the product table (which switches to cards on small screens).

## Deployment configuration (no deployment performed)

Set `ConnectionStrings__DefaultConnection`, `Jwt__SigningKey`, SMTP settings, and
`Email__WebsiteBaseUrl` using environment configuration/secrets. The website base URL
must be an absolute HTTPS URL. Use real SMTP outside development; Mailpit is local
only. Keep HTTPS end-to-end or configure trusted reverse-proxy forwarding before
hosting behind a proxy. Persist/protect ASP.NET Core Data Protection keys when
hosting in containers or across multiple instances so cookies survive restarts.

Email confirmation was already required. The subsequent authentication milestone
adds recoverable SMTP failure handling and a rate-limited confirmation-resend UI.
Durable delivery/automatic retry remains future work. Password recovery endpoints already
exist in the API; this milestone does not add recovery pages. Scheduled cleanup,
new logging infrastructure and simulation pages are outside this milestone.

## File inventory

Modified:

```text
src/FinSight.Api/Program.cs
src/FinSight.Api/appsettings.Development.json
src/FinSight.Api/Controllers/AuthController.cs
src/FinSight.Api/Controllers/ProductsController.cs
src/FinSight.Api/Controllers/PricingSimulationsController.cs
src/FinSight.Api/ExceptionHandling/ProblemDetailsAuthorizationMiddlewareResultHandler.cs
src/FinSight.Infrastructure/DependencyInjection.cs
src/FinSight.Infrastructure/Identity/AuthenticationService.cs
src/FinSight.Infrastructure/Identity/EmailOptions.cs
```

Created:

```text
docs/WEBSITE.md
src/FinSight.Application/Common/Interfaces/IWebsiteAuthenticationService.cs
src/FinSight.Infrastructure/Identity/WebsiteAuthenticationService.cs
src/FinSight.Infrastructure/Identity/WebsiteClaimsPrincipalFactory.cs
src/FinSight.Api/Website/Controllers/WebsiteController.cs
src/FinSight.Api/Website/Controllers/HomeController.cs
src/FinSight.Api/Website/Controllers/AccountController.cs
src/FinSight.Api/Website/Controllers/ProductPagesController.cs
src/FinSight.Api/Website/Models/AccountModels.cs
src/FinSight.Api/Website/Models/ProductModels.cs
src/FinSight.Api/Website/WebsiteExceptionFilter.cs
src/FinSight.Api/Views/_ViewImports.cshtml
src/FinSight.Api/Views/_ViewStart.cshtml
src/FinSight.Api/Views/Shared/_Layout.cshtml
src/FinSight.Api/Views/Shared/Error.cshtml
src/FinSight.Api/Views/Home/Index.cshtml
src/FinSight.Api/Views/Account/Register.cshtml
src/FinSight.Api/Views/Account/Login.cshtml
src/FinSight.Api/Views/Account/CheckEmail.cshtml
src/FinSight.Api/Views/Account/ConfirmEmail.cshtml
src/FinSight.Api/Views/ProductPages/Index.cshtml
src/FinSight.Api/Views/ProductPages/Form.cshtml
src/FinSight.Api/wwwroot/css/site.css
tests/FinSight.UnitTests/Website/AccountControllerTests.cs
tests/FinSight.IntegrationTests/WebsiteTests.cs
```

The pre-existing uncommitted edit to
`src/FinSight.Domain/Entities/PricingSimulation.cs` was preserved and is not part of
this website implementation. Nothing was committed, pushed or deployed.
