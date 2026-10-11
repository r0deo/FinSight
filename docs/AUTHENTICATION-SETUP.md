# Email confirmation and Google sign-in

For the current Render Free/Neon pilot, follow [DEPLOYMENT.md](DEPLOYMENT.md). It
replaces production disk key storage with certificate-encrypted PostgreSQL storage
and documents SMTP restrictions and explicit email-disabled pilot mode.

## What is implemented

Email/password remains supported. Both website password login and API token issuance
require confirmed email. Identity creates 24-hour confirmation tokens. Links use
`Email:WebsiteBaseUrl`, never the incoming Host header. Opening a link transfers its
token into a short-lived, encrypted HttpOnly cookie and redirects to a clean URL;
confirmation requires an antiforgery-protected POST. Tokens/links are not rendered
in page HTML or application log messages. Resend returns the same outcome for
unknown, confirmed, and unconfirmed addresses, including delivery failures; it is
limited to three requests per IP per 15 minutes. Limits are per application instance.

Google uses the official ASP.NET Core Google middleware, state/correlation cookies,
PKCE and its authenticated userinfo backchannel. No client-submitted Google tokens
or email claims are trusted. Accounts are identified by `(Google, provider subject)`.
Only an explicit `email_verified=true` from userinfo confirms an email automatically.
Otherwise normal email confirmation is required. Google access tokens are not saved.

A new Google identity gets a ten-minute external cookie, NOT a tenant session. It
must submit valid Business details first. Business, passwordless Owner and external
login are committed in one PostgreSQL transaction. An advisory lock serializes
same-subject signup retries; Identity unique constraints are the final safeguard.
Local/Google role provisioning also shares a transaction lock. Existing associations
reuse their original account, Business and roles even if Google returns a new email.

Matching a local email never links an account. The person must sign in to the existing
FinSight account, visit `/account/google/link`, and explicitly start another Google
round trip. Identity's XSRF value binds linking to that authenticated account. A Google
identity already associated with another account cannot be reassigned.

Application defines confirmation/Google abstractions and validated MediatR onboarding
and resend commands. Infrastructure owns Identity, SMTP, OAuth, transaction handling
and key storage. MVC only coordinates pages and Application calls. Existing API JWT,
website cookies, Owner/Manager checks, tenant repositories, product operations and
simulation snapshots remain unchanged. No schema change or new migration is needed.

## Email provider: Brevo over authenticated SMTP

The existing sender was `SmtpEmailSender` (`System.Net.Mail`), already capable of
authenticated SMTP. Development pointed it to Mailpit, which only captures messages.
No real provider credentials were configured. The implementation reuses that sender
and supplies a production Brevo configuration: `smtp-relay.brevo.com:587`, STARTTLS.
This avoids another SDK and still permits another authenticated SMTP provider through
configuration. Gmail, Outlook and other valid recipient domains are not filtered.
Production startup rejects missing credentials, disabled TLS and a local public URL.

1. Create a Brevo account and enable transactional sending; complete any account review.
2. Add a sending domain you control and a sender such as `no-reply@your-domain`.
   Do not use a consumer `@gmail.com` address as the sending domain.
3. Copy the exact DNS names/values from Brevo: ownership TXT (Brevo code), DKIM
   (one TXT or two CNAME records depending on the account), and DMARC TXT. Merge with
   an existing DMARC policy; do not create duplicate DMARC records. Verify in Brevo
   after DNS propagation. Standard shared SMTP domain authentication does not require
   replacing your MX or inventing a new SPF record; use additional records only if
   Brevo's chosen dedicated-IP/branded-domain setup explicitly requires them.
4. Get the SMTP login and generate an SMTP key, not an API key or Gmail password.
5. Configure the keys below and send a real test. A successful SMTP submission is not
   proof of inbox delivery: check provider events, recipient inbox/spam and headers.

See [Brevo SMTP instructions](https://help.brevo.com/hc/en-us/articles/7924908994450-Send-transactional-emails-using-Brevo-SMTP)
and [domain/DNS instructions](https://help.brevo.com/hc/en-us/articles/12163873383186-Authenticate-your-domain-with-Brevo-Brevo-code-DKIM-DMARC).

Delivery remains synchronous with a 30-second timeout. SMTP failures leave the already
committed account intact and return the normal check-email/resend outcome. A later
resend can recover. Durable queues/automatic email retries are not implemented.

## Configuration keys (no secret values)

Store local secrets using **Manage User Secrets** for `src/FinSight.Api` or
`dotnet user-secrets set KEY VALUE --project src/FinSight.Api`. Avoid entering secrets
into recorded terminals/shared shell history. Use your deployment secret store or
environment variables in production. Replace `:` with `__` for environment variables.

| Key | Required value/purpose |
| --- | --- |
| `Email:WebsiteBaseUrl` | Trusted HTTPS origin only, no path/query; local `https://localhost:7080` |
| `Email:Host` | `smtp-relay.brevo.com` for real Brevo sending |
| `Email:Port` | `587` (STARTTLS; not implicit-TLS port 465) |
| `Email:EnableSsl` | `true` for real SMTP |
| `Email:FromAddress` | Verified sender on your domain |
| `Email:Username` | Brevo SMTP login |
| `Email:Password` | Brevo SMTP key; secret |
| `Authentication:Google:Enabled` | `true` only after supplying Google client settings |
| `Authentication:Google:ClientId` | Web application OAuth client ID |
| `Authentication:Google:ClientSecret` | OAuth client secret; secret |
| `DataProtection:KeysPath` | Development-only directory; production now uses PostgreSQL |
| `DataProtection:CertificatePath` | Production PFX file with a private key, outside source control |
| `DataProtection:CertificatePassword` | PFX password from secret storage |
| `ConnectionStrings:DefaultConnection` | Existing PostgreSQL connection string; production credential is secret |
| `Jwt:SigningKey` | Existing signing key; do not rotate merely for this feature |
| `AllowedHosts` | Actual production hostname(s), not `*`, when deploying |

`appsettings.Production.json` contains only the non-secret Brevo host, port and TLS
defaults. Development retains Mailpit. To test real sending locally, override all
SMTP settings in user secrets; do not edit development credentials into source.
Google is disabled by default and its buttons stay hidden until enabled. Enabling
Google without its credentials fails startup instead of showing a broken button.

## Google Console setup

1. Open Google Cloud Console; create/select the FinSight project. Open **Google Auth
   Platform** and complete **Branding** (app name, support email, developer contact).
2. Under **Audience**, choose **External** for restaurant/cafe customers outside your
   Workspace organization. **Internal** would exclude those customers. Add your
   QA accounts as test users while configuring the project.
3. Under **Data Access**, request only `openid`, `email`, `profile` (the Console may
   display the equivalent `userinfo.email` / `userinfo.profile` scope names). Do not
   enable Gmail permissions. The application sends exactly these three scopes.
4. Under **Clients**, create an OAuth client of type **Web application**, preferably
   separate development and production clients. Add this exact development
   **Authorized redirect URI**: `https://localhost:7080/signin-google`.
   JavaScript origins are not required by this server-side flow; no browser OAuth SDK
   is used. Do not register `/account/google/callback` as the provider redirect URI.
5. Copy Client ID/Client secret into the settings above, then set Enabled to true.
   Run the HTTPS profile and visit `https://localhost:7080/account/login`.
6. Production has **no real domain configured in this repository**. Once you supply
   the domain, set `Email:WebsiteBaseUrl` to that exact HTTPS origin and register
   **that origin + `/signin-google`** in the production Google client. For example,
   if your configured origin is `https://app.your-owned-domain`, the callback is
   `https://app.your-owned-domain/signin-google` (illustrative, not an actual configured
   FinSight address). Paths/ports/case must match. Add your owned domain in Branding
   and complete domain ownership/branding verification requested by Google.
7. Before customer launch, publish the External app and complete applicable branding
   verification, using real homepage/privacy/terms/support details. Do not configure
   nonexistent legal pages. No such public domain or legal-page content was supplied.

Current Google documentation has an exception to the usual Testing allowlist: apps
requesting only `openid`, `email`, `profile` can be used without allowlisting each user.
Testing can still show testing UI, and Workspace administrators can block access.
Do not tell customers there is an unavoidable 100-user limit for this scope set.
FinSight requests no offline access and stores no Google refresh tokens; Google
testing refresh-token expiry is not the lifetime of a FinSight cookie/JWT.
See [Google app states](https://developers.google.com/identity/protocols/oauth2/production-readiness/overview)
and [Microsoft's Google setup guide](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-10.0).

OAuth routes require the configured HTTPS origin and reject unexpected hosts. For
deployment use HTTPS end-to-end to the app. If TLS terminates at a reverse proxy,
configure narrowly trusted forwarded headers before deployment (not arbitrary
client-supplied headers); this repository does not assume a proxy IP/topology.

## Data Protection and logging

Development persists keys under `%LOCALAPPDATA%\FinSight\DataProtection-Keys` and
encrypts them with Windows DPAPI for the same OS user. Non-Windows development must
protect directory access or configure a certificate. Production now stores the key
ring in PostgreSQL and requires a PFX certificate supplied independently of the DB.
See DEPLOYMENT.md for Render secret-file support, migration and rotation instructions.
All instances of the same deployment need the same database, certificate and application
name (`FinSight`). Keep different deployments' key stores separate. Preserve
old decryption certificates during key rotation; never simply delete the old key ring.

Moving from the previous implicit key setup to this explicit one may invalidate
existing cookies/confirmation links once. Sign in/request a new link after the
upgrade. Subsequent restarts retain the configured keys. Unit coverage reconstructs
the provider and successfully decrypts data from the earlier instance.

Application confirmation failures log only a fixed message, without exception
payloads, email addresses or tokens. ASP.NET request logging remains Warning-level.
At deployment also redact/omit query strings, Cookie/Set-Cookie and Authorization
headers in proxy/access/APM logs: incoming confirmation URLs necessarily contain a
token before the application cleans the URL. Do not enable request-body logging for
authentication endpoints or OAuth debug logs in production.

## Run and verify

From `D:\FinSight`:

```powershell
dotnet restore FinSight.slnx
dotnet build FinSight.slnx
dotnet test FinSight.slnx --no-build
docker compose up -d
dotnet run --project src/FinSight.Api --launch-profile https
```

Docker Desktop is required for the isolated PostgreSQL integration tests. No new
migration is needed. On a database not yet initialized, apply the already-existing
migrations with `dotnet ef database update --project src/FinSight.Infrastructure --startup-project src/FinSight.Api`.

Useful pages: `/account/register`, `/account/login`, `/account/resend-confirmation`,
`/account/confirmation-success`, and (signed in) `/account/google/link` on
`https://localhost:7080`. The Google business page is only usable after a valid
external callback. Local Mailpit inbox: `http://localhost:8025`.

Automated verification: 66 unit tests and 26 PostgreSQL integration tests passed,
zero skipped. Final build passed with zero warnings/errors. EF's
`migrations has-pending-model-changes` reported no pending model changes. Tests
exercise the real Google middleware, state, correlation, PKCE,
external/website cookies and Identity, with **mocked Google HTTP responses** and a
**fake email sender**. Coverage includes unconfirmed website/API rejection, valid,
invalid/expired links, send failure, generic/rate-limited resend, verified/unverified
Google onboarding, concurrent duplicate retries, email-match non-linking, explicit
linking, switched-user XSRF rejection, cancellation, unsafe return URLs, and existing
product/simulation authorization and tenant isolation.

**Not verified live:** Brevo/Gmail delivery, real Google login/consent, production DNS,
TLS/proxy or production certificate deployment. Credentials/domain settings are not
available. Nothing was committed, pushed or deployed.

Manual checklist after supplying configuration:

- Register with a real Gmail address and another provider. Check received messages,
  inbox/spam and provider delivery events; verify the sender domain is authenticated.
- Before confirming, try website password login and API `/api/auth/login`: both must
  reject. Open the email link, confirm, then sign in; alter a token and check the
  invalid-link page. Request a resend for known and unknown addresses.
- Try Google with a new identity: Business setup must precede product access. Repeat
  sign-in and verify the same tenant. Cancel consent and verify the clean error page.
- Try Google matching an existing local email: no automatic link. Sign into the local
  account, explicitly link Google, sign out and sign in with Google again.
- Verify a second business cannot access the first business's products/simulations;
  API cookie-only requests stay 401 and role failures remain 403, without redirects.
- Restart the app without changing keys and confirm an existing cookie and an unused
  confirmation link still work. Check auth pages on mobile and desktop.

## Changed-file inventory for this authentication milestone

The previous uncommitted website milestone and Domain edit were preserved. Relative
to that starting point, this milestone creates/modifies:

```text
.gitignore
docs/AUTHENTICATION-SETUP.md
docs/WEBSITE.md
src/FinSight.Application/Common/Interfaces/IEmailConfirmationService.cs
src/FinSight.Application/Common/Interfaces/IGoogleSignInService.cs
src/FinSight.Application/Features/Authentication/Commands/ResendConfirmation/ResendConfirmationCommand.cs
src/FinSight.Application/Features/Authentication/Commands/GoogleOnboarding/GoogleOnboardingCommand.cs
src/FinSight.Infrastructure/FinSight.Infrastructure.csproj
src/FinSight.Infrastructure/DependencyInjection.cs
src/FinSight.Infrastructure/Identity/AuthenticationService.cs
src/FinSight.Infrastructure/Identity/SmtpEmailSender.cs
src/FinSight.Infrastructure/Identity/EmailConfirmationService.cs
src/FinSight.Infrastructure/Identity/EmailOptionsValidator.cs
src/FinSight.Infrastructure/Identity/DataProtectionRegistration.cs
src/FinSight.Infrastructure/Identity/GoogleSignInOptions.cs
src/FinSight.Infrastructure/Identity/GoogleAuthenticationRegistration.cs
src/FinSight.Infrastructure/Identity/GoogleSignInService.cs
src/FinSight.Infrastructure/Identity/IdentityRoleProvisioner.cs
src/FinSight.Api/Program.cs
src/FinSight.Api/appsettings.json
src/FinSight.Api/appsettings.Production.json
src/FinSight.Api/Website/ConfirmationTicketStore.cs
src/FinSight.Api/Website/Controllers/AccountController.cs
src/FinSight.Api/Website/Controllers/GoogleAccountController.cs
src/FinSight.Api/Website/Models/AccountModels.cs
src/FinSight.Api/Website/Models/GoogleBusinessViewModel.cs
src/FinSight.Api/Views/Shared/_Layout.cshtml
src/FinSight.Api/Views/Shared/_GoogleButton.cshtml
src/FinSight.Api/Views/Account/Login.cshtml
src/FinSight.Api/Views/Account/Register.cshtml
src/FinSight.Api/Views/Account/CheckEmail.cshtml
src/FinSight.Api/Views/Account/ConfirmEmail.cshtml
src/FinSight.Api/Views/Account/ConfirmationSuccess.cshtml
src/FinSight.Api/Views/Account/InvalidConfirmation.cshtml
src/FinSight.Api/Views/Account/ResendConfirmation.cshtml
src/FinSight.Api/Views/Account/ResendSent.cshtml
src/FinSight.Api/Views/GoogleAccount/Business.cshtml
src/FinSight.Api/Views/GoogleAccount/ExistingAccount.cshtml
src/FinSight.Api/Views/GoogleAccount/Link.cshtml
src/FinSight.Api/Views/GoogleAccount/Error.cshtml
src/FinSight.Api/Views/GoogleAccount/Unavailable.cshtml
tests/FinSight.UnitTests/Website/AccountControllerTests.cs
tests/FinSight.UnitTests/Website/AuthenticationConfigurationTests.cs
tests/FinSight.IntegrationTests/Infrastructure/FinSightApiFactory.cs
tests/FinSight.IntegrationTests/Infrastructure/FakeEmailSender.cs
tests/FinSight.IntegrationTests/Infrastructure/GoogleApiFactory.cs
tests/FinSight.IntegrationTests/Infrastructure/WebsiteTestClient.cs
tests/FinSight.IntegrationTests/AuthenticationAndPricingTests.cs
tests/FinSight.IntegrationTests/SavedPricingSimulationTests.cs
tests/FinSight.IntegrationTests/WebsiteTests.cs
tests/FinSight.IntegrationTests/EmailConfirmationTests.cs
tests/FinSight.IntegrationTests/GoogleSignInTests.cs
```

Added package: `Microsoft.AspNetCore.Authentication.Google` **10.0.12**, compatible
with the project's `net10.0` framework. No email SDK or database migration was added.
