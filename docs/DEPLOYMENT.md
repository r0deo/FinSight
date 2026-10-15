# Free pilot deployment: Render Docker + Neon PostgreSQL

This is preparation for a small pilot, not an always-on production SLA. No accounts
were created and nothing was purchased, deployed, committed or pushed. Your actual
Render hostname and external credentials are still required. This guide supersedes
the earlier production local-disk key-storage instructions.

## Deployment architecture

- Root `Dockerfile`: .NET 10 SDK build, ASP.NET Core 10 runtime, non-root user.
  Publish includes compiled MVC views and static assets. `.dockerignore` allowlists
  build inputs, excluding secrets, certificates, development settings and local data.
- The host reads `PORT` (image default `10000`) and binds `0.0.0.0`.
- Non-development PostgreSQL requires `SSL Mode=VerifyFull`, validating both the
  certificate chain and hostname. Never use `Trust Server Certificate=true` or
  downgrade to `SSL Mode=Require`.
- Production Data Protection keys are certificate-encrypted XML in PostgreSQL's
  `DataProtectionKeys` table. The private certificate is supplied separately from
  the database/image. Development keeps its local directory/Windows DPAPI behavior.
- `/health` returns only `200`, `text/plain`, `ok`. It is liveness, not database
  readiness: no database query on every probe, avoiding needless Neon wake-ups.
- Existing cookie/JWT separation, confirmation, Google onboarding, roles, tenant
  filtering and simulation rules remain unchanged. Startup never applies migrations.

## 1. Neon Free setup

1. In [Neon Console](https://console.neon.tech), create a Free project in a region
   close to your intended Render region. Do not enable paid compute/features.
2. Select/create the pilot branch, database and database role. Keep this separate
   from local development. Check current Free allowances in the Console.
3. Click **Connect**, select branch/database/role and switch Connection pooling off:
   save this **direct hostname** for migrations. Switch pooling on to copy the
   **pooled hostname** for runtime (normally contains `-pooler`).
4. Use Npgsql key/value format, not a `postgresql://` URL. Replace placeholders:

```text
Host=<actual-pooled-neon-host>;Port=5432;Database=<database>;Username=<role>;Password=<password>;SSL Mode=VerifyFull;Channel Binding=Require;Pooling=true;Maximum Pool Size=10;Timeout=30;Command Timeout=30
```

For migrations substitute the actual DIRECT hostname. Quote password values using
Npgsql connection-string quoting if they contain semicolons/quotes; URL encoding is
not connection-string quoting. Store the full string as a secret. The runtime image
has public CA roots. Fix hostname/CA errors instead of disabling TLS verification.

Using the Neon project role initially is convenient but gives the app DDL permissions.
Before a broader rollout, use a restricted runtime role with the necessary business,
Identity and DataProtectionKeys permissions. Runtime needs key-ring read/insert access.
Existing application advisory locks are transaction-scoped, compatible with transaction
pooling. Schema migrations use a direct connection.
[Neon pooling](https://neon.com/docs/connect/connection-pooling),
[Npgsql TLS](https://www.npgsql.org/doc/security.html).

## 2. Explicit migrations BEFORE launch

### If Neon provides `DATABASE_URL` / `DATABASE_URL_POOLED`

Use the direct `DATABASE_URL` with the local masked helper. Run these commands in
your own interactive PowerShell window, not a recorded shared terminal:

```powershell
dotnet build tools/FinSight.NeonMigrate/FinSight.NeonMigrate.csproj
dotnet tools/FinSight.NeonMigrate/bin/Debug/net10.0/FinSight.NeonMigrate.dll
```

Paste only the URL into its masked prompt (not into chat or the command line).
The helper rejects pooled/non-Neon hosts, URL-decodes credentials exactly once,
quotes Npgsql values safely, and enforces `VerifyFull` TLS with channel binding.
The driver validates the certificate and hostname while opening the connection;
GSS negotiation is disabled to require TLS. Do not use `pg_stat_ssl` to verify this
client connection: behind Neon's proxy it describes a different, backend connection.
It displays only the target host/database for your local confirmation; type `APPLY`
only after checking the intended branch/database in Neon. It applies existing
migrations, verifies persisted migration history and the DataProtectionKeys table,
then writes a credential-free `migration-result.json` beside its executable under
the ignored `bin` directory. It does not store the URL in environment variables or
files, change development settings, seed data, create migrations or start the website.
Credentials necessarily exist in process memory while connecting; close the helper
when finished. Do not share memory dumps or record credential entry.

If the helper fails, its report contains only a safe category/SQLSTATE, never raw
provider exceptions. Re-run against the same confirmed target to reconcile history;
do not assume a failed verification means no migration was applied. This helper is
for migrations only; Render runtime configuration still uses the pooled Npgsql string.

### If you already have an Npgsql key/value connection string

Run from `D:\FinSight` in a dedicated PowerShell window with .NET 10 SDK:

```powershell
dotnet tool restore
dotnet restore FinSight.slnx
dotnet build FinSight.slnx
$env:DOTNET_ENVIRONMENT = 'Production'
$neonConnection = Read-Host 'Paste the DIRECT Neon Npgsql connection string' -AsSecureString
$env:ConnectionStrings__DefaultConnection = [System.Net.NetworkCredential]::new('', $neonConnection).Password
try {
    dotnet ef migrations list --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Migration inspection failed.' }
    dotnet ef database update --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Migration application failed.' }
    dotnet ef migrations has-pending-model-changes --project src/FinSight.Infrastructure --startup-project src/FinSight.Api --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Model verification failed.' }
} finally {
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    Remove-Item Env:DOTNET_ENVIRONMENT -ErrorAction SilentlyContinue
    Remove-Variable neonConnection -ErrorAction SilentlyContinue
}
```

The design-time factory reads environment configuration independently of the host:
no Google/SMTP/JWT/certificate credentials are needed just to migrate. Keep secrets
out of command arguments, transcripts and screenshots. The earlier
**`20261011105742_PersistDataProtectionKeys`** migration adds the key-ring table.
The later **`20261013105643_AddPricingDecisionAssumptions`** migration adds
pricing-decision snapshot fields and permits an unknown expected quantity on
saved simulations. **`20261013120101_AddWhatIfSimulations`** adds a separate
table for saved cost-and-demand shock scenarios.
**`20261013163254_AddBusinessCosts`** is required for `/business-costs` and
monthly cost context in previews. **`20261014125743_AddBusinessCostMonthReviews`**
adds owner review declarations and atomic monthly versions. Apply all pending
migrations explicitly before serving this release. No application startup migration
or empty-data fallback is implemented.

### Business Costs 500: release checklist

An authenticated request against the schema before `AddBusinessCosts` reproduces
PostgreSQL SQLSTATE `42P01`: `relation "BusinessCosts" does not exist`. Applying
the migrations restores the page. This was reproduced on disposable local PostgreSQL;
production logs and production migration history have **not** been inspected.
If Render reports this error (or a missing `BusinessCostMonths` table), check the
database target and migration history rather than changing authentication or hiding
the error as an empty month. `/health` is liveness only and cannot confirm schema readiness.

1. Keep automatic deployment disabled while preparing the release. Back up the
   intended Neon branch and verify that the runtime pooled connection and migration
   direct connection target the same branch/database.
2. From the source version containing both migrations, run the masked helper in
   your own interactive PowerShell window from `D:\FinSight`:

   ```powershell
   dotnet build tools/FinSight.NeonMigrate/FinSight.NeonMigrate.csproj
   if ($LASTEXITCODE -eq 0) {
       dotnet tools/FinSight.NeonMigrate/bin/Debug/net10.0/FinSight.NeonMigrate.dll
   }
   ```

3. Enter the **direct** Neon URL only at the masked prompt. Verify the displayed
   host/database, then type `APPLY`. The helper applies all pending existing
   migrations in order and verifies history. Do not generate another migration,
   reset the database, or copy credentials into chat/command arguments.
4. Confirm success and history containing `20261013163254_AddBusinessCosts` and
   `20261014125743_AddBusinessCostMonthReviews`. The helper's credential-free
   result is under its ignored `bin/Debug/net10.0` directory. With authorized SQL
   console access, a read-only additional check is:

   ```sql
   SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
   SELECT to_regclass('public."BusinessCosts"'), to_regclass('public."BusinessCostMonths"');
   ```

5. Only after successful migration verification, manually deploy the matching
   release through your approved workflow. Sign in, use the Business Costs
   navigation link, select a month, and check listing/CRUD/review. Confirm that a
   second business cannot access the first business's entries. If it still fails,
   inspect the actual Render exception and SQLSTATE; do not assume every 500 has
   the same cause. Never share connection strings or full sensitive logs.

These steps are instructions for the operator, **not actions performed against
production by the implementation agent**. The review migration adds one table;
it neither changes recorded amounts nor marks existing months complete. Old
versions can continue using their existing tables, but do not downgrade/drop
tables containing customer records.
Do not roll back the key-ring migration on a used database: dropping that table
invalidates protected data. Back up pilot data before updates.

Render Free does not provide the paid pre-deploy command/shell workflow. Run migration
commands on your trusted machine or a separately approved release process, not inside
the Docker startup command. [Render deploy commands](https://render.com/docs/deploys)

Local development remains separate:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet ef database update --project src/FinSight.Infrastructure --startup-project src/FinSight.Api
Remove-Item Env:DOTNET_ENVIRONMENT
```

### Product-cost helper release

Apply **`20261015141948_AddProductCostBreakdown`** before serving the product-cost
helper release. It adds only a nullable `Products.CostBreakdown` text column;
existing manual costs and simulation snapshots are not changed. Use the same
masked direct-Neon migration helper above, verify its migration history, then
release the matching application version. No automatic startup migration is added.
See [PRODUCT_COST_HELPER.md](PRODUCT_COST_HELPER.md) for limits, local verification,
manual checks and the complete change manifest. Production is not migrated by the
implementation task.

## 3. Provision the Data Protection certificate

This private encryption certificate is NOT the website HTTPS certificate. It may be
self-signed and requires no purchased domain. Render manages public HTTPS separately.

On a trusted machine with OpenSSL, in a private directory outside the repository:

```text
openssl req -x509 -newkey rsa:3072 -sha256 -days 1825 -subj "/CN=FinSight Pilot Data Protection" -keyout finsight-dp-private.pem -out finsight-dp-public.pem
openssl pkcs12 -export -inkey finsight-dp-private.pem -in finsight-dp-public.pem -out finsight-dp.pfx
openssl base64 -A -in finsight-dp.pfx -out finsight-dp.pfx.b64
```

Passwords are prompted, not included in command arguments. Save the PFX export
password securely. Base64 is only encoding: the `.b64` file is secret too. Back up
the private certificate and password independently of the database. Retain the SAME
certificate across redeploys/restarts; never generate it during build/startup.

In Render **Environment → Secret Files**, add `finsight-dp.pfx.b64`, pasting its full
base64 contents. It is mounted at `/etc/secrets/finsight-dp.pfx.b64`; the app decodes
it in memory. Configure its path/password below. No private key is stored in the
database or image. [Render secret files](https://render.com/docs/configure-environment-variables#secret-files)

All instances use the same database, certificate and application name `FinSight`.
Authentication needs the database available to load the key ring on a fresh instance.
The health endpoint alone does not prove this works. Losing the private certificate
or key-ring table invalidates cookies and unconsumed confirmation tokens.

For planned certificate rotation, upload the new active certificate and retain the
old base64 secret file. Set `DataProtection__PreviousCertificateBase64Files__0` to its
path (additional files use `__1`, etc.). All configured PFX files use
`DataProtection__CertificatePassword`; re-export old PFXs under that password if
necessary without changing their keys. Existing rows are not automatically re-encrypted.
Keep old certificates as long as the corresponding rows/backups are retained. Do not
discard them merely because a cookie expired. Compromise needs an incident plan.

No local developer key ring is imported into the new pilot. An already-used disk-based
production deployment would need a separately reviewed import to preserve sessions.

## 4. Render Free setup (manual; not performed)

1. Review the changes. When you later authorize publication, make the selected code
   available to your Git provider. Render cannot build uncommitted local files.
2. Choose **New → Web Service**, connect the intended FinSight repository/branch,
   select **Docker**, choose a region near Neon and select the **Free** instance.
3. Keep repository root as build context `.`, Dockerfile `./Dockerfile`, and leave
   Docker command override empty. Disable auto-deploys for controlled pilot releases.
4. Set health path `/health`. Do not attach a disk, create Render Postgres, or add
   migration commands to startup. Add the variables and secret file below.
5. Use the actual assigned `onrender.com` hostname shown by Render. If creation assigns
   a different hostname than expected, correct origin/AllowedHosts/Google callback
   before treating the pilot as launched. Deploy manually after applying migrations.
6. Check the internal listener uses `0.0.0.0:10000` (or assigned PORT), not localhost.
   Internal HTTP is expected because Render terminates public TLS.

| Environment variable | Pilot value |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` (image default) |
| `Hosting__Platform` | `Render` |
| `PORT` | `10000` or Render's assigned value |
| `AllowedHosts` | `<actual-render-host>.onrender.com` only; no wildcard/scheme/path |
| `Email__WebsiteBaseUrl` | `https://<actual-render-host>.onrender.com`; no path/query |
| `ConnectionStrings__DefaultConnection` | Secret pooled Neon Npgsql string with `SSL Mode=VerifyFull` |
| `Jwt__SigningKey` | Stable secret random key, at least 32 characters |
| `DataProtection__CertificateBase64File` | `/etc/secrets/finsight-dp.pfx.b64` |
| `DataProtection__CertificatePassword` | Secret PFX export password |
| `Email__Enabled` | `false` until real Brevo delivery is configured/tested |
| `Authentication__Google__Enabled` | `false` until real credentials/callback are ready |
| `Authentication__Google__ClientId` | Google Web application OAuth client ID when enabling |
| `Authentication__Google__ClientSecret` | Secret Google client secret when enabling |

Generate a random pilot JWT key once (e.g. 64 random characters from a password
manager), save it and reuse it across deploys. Do not configure production KeysPath,
put certificates in images, set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, or override
Kestrel endpoints/URL variables. Google-disabled pages do not show a broken button.

### HTTPS/proxy boundary

Render provides HTTPS certificates and redirects public HTTP to HTTPS. The explicit
Render profile checks the request Host against the configured origin, then derives
HTTPS from that deployment configuration. It never trusts arbitrary `X-Forwarded-*`
headers or guesses proxy CIDRs. This profile is ONLY for Render's TLS-terminated edge,
not a directly exposed HTTP server. Google links retain the correct HTTPS origin.

Client-IP forwarding is intentionally not accepted. Authentication/resend limits may
therefore be shared by users behind the same Render proxy, causing 429s during bursts.
Do not disable limits to work around this; a future client-IP integration needs a
verified proxy contract. Secure cookies, antiforgery and Bearer API behavior remain.
[Render Docker](https://render.com/docs/docker),
[port binding](https://render.com/docs/web-services#port-binding),
[TLS behavior](https://render.com/docs/tls),
[health checks](https://render.com/docs/health-checks).

## 5. Google on the Render HTTPS address

1. In Google Cloud Console, select/create a project. Under Google Auth Platform,
   choose **External** audience for customers, not Internal-only Workspace access.
   Add QA accounts while setting up and keep only `openid`, `email`, `profile` scopes.
2. In **Clients**, create a **Web application** OAuth client for the pilot.
3. Add exactly `https://<actual-render-host>.onrender.com/signin-google` as an authorized
   redirect URI. Not `/account/google/callback`, an API URL, HTTP or a wildcard.
4. Save ID/secret in Render, then enable Google. Supply truthful branding/support
   information. You do not own the `onrender.com` root domain; do not claim ownership.
   If branding verification requests ownership you cannot demonstrate, keep the pilot
   in Testing while resolving it rather than claiming a verified public brand.
5. Test consent/cancellation, new Business onboarding, existing sign-in and explicit
   linking. Provider-unverified email still requires a working confirmation sender.

Google documents a Testing allowlist exception for basic identity scopes, but testing
UI and Workspace policies may still restrict users. No Gmail permissions are requested.
See [Google app states](https://developers.google.com/identity/protocols/oauth2/production-readiness/overview)
and [authentication setup](AUTHENTICATION-SETUP.md#google-console-setup).

## 6. Email remains a separate blocker

Mailpit is development-only: never expose it or use localhost for production SMTP.
With `Email__Enabled=false`, delivery is explicitly unavailable. Registration retains
the unconfirmed account, but password login/API token issuance stays blocked until
confirmation. The website displays a pilot notice. Existing confirmed users still
work. Verified Google users can onboard without SMTP; unverified ones cannot.
Password recovery delivery is unavailable too.

Brevo remains the provider. Render Free blocks outbound SMTP 25/465/587. Brevo supports
2525: once you have a verified sender, configure `Email__Host=smtp-relay.brevo.com`,
`Email__Port=2525`, `Email__EnableSsl=true` (STARTTLS), `Email__FromAddress`,
`Email__Username`, `Email__Password`, and `Email__Enabled=true`. Verify real delivery
from Render; do not disable TLS if connection fails. Port 2525 has not been tested live.

The Render URL is website hosting, NOT an email-sending domain you own. Without an
owned/provider-approved sender domain, Brevo verification remains unresolved; do not
use `onrender.com` for FromAddress. No domain purchase, provider switch or HTTP email
API implementation is part of this milestone.
[Render Free limits](https://render.com/docs/free),
[Brevo ports](https://help.brevo.com/hc/en-us/articles/10905415650322-Which-SMTP-port-should-I-use-Port-587-465-or-2525).

## 7. Verification

```powershell
dotnet build FinSight.slnx
dotnet test FinSight.slnx --no-build
docker build --tag finsight:pilot .
dotnet run --project tools/FinSight.ContainerSmoke -- finsight:pilot
```

Executed locally: **81 unit tests and 27 PostgreSQL integration tests passed**. Docker
image build and the smoke tool passed. The restart integration test decrypts a cookie
and confirms an Identity token after recreating services; a wrong certificate cannot
decrypt. The smoke tool verified production-image MVC/static assets, liveness,
non-root execution, excluded development settings, real VerifyFull PostgreSQL TLS,
confirmation, cookie/JWT separation, host rejection, encrypted key storage and cookie
survival after replacing the app container.

Final solution build passed with zero warnings/errors; EF reported no pending model
changes after adding the key-ring migration.

The tool uses isolated disposable containers/network and temporary test certificates,
never Neon or your existing local database. Migrations there are explicit test setup,
not app startup. It manually carries Secure cookies on the loopback HTTP hop emulating
Render's TLS-terminated internal hop; this does not test Render's actual edge. Test
resources are cleaned up. No live SMTP/Google credentials are used.

### Manual launch checks

- Check migration history includes `PersistDataProtectionKeys`,
  `AddPricingDecisionAssumptions`, and `AddWhatIfSimulations` before serving
  the corresponding website pages.
- Check HTTPS `/health`, HTTP→HTTPS redirect, `/account/login`, `/css/site.css`, and
  a real database-backed operation. Wrong hosts should be rejected.
- Complete real Google Business onboarding; create/edit products and preview pricing.
  A second business must not access the first business's products/simulations.
- Cookie-only API requests must remain JSON 401; role failures must stay 403. Check
  Google cancellation and unconfirmed accounts fail safely.
- Redeploy with the SAME database, DP certificate and JWT key. Verify existing cookies
  and unused confirmation tokens within their lifetimes. Never log key XML/tokens.
- Check wake-up behavior and usage dashboards. Render Free sleeps when idle and can
  cold-start slowly; Neon can also suspend compute. Keep backups and explain pilot
  limitations. Do not add artificial keep-alive jobs to defeat Free-tier behavior.

Remaining manual setup: Render/Neon accounts, approved source branch, actual hostname,
Neon credentials, certificate/password/backups, stable JWT key, Google OAuth/consent,
and later verified Brevo sender/SMTP credentials. Real Render/Neon deployment,
Neon-specific connectivity, public TLS, live Google consent and email delivery have
NOT been tested. No remote migration was applied.

## Changed files in this deployment milestone

```text
Dockerfile
.dockerignore
.gitignore
docs/DEPLOYMENT.md
docs/AUTHENTICATION-SETUP.md
src/FinSight.Api/Program.cs
src/FinSight.Api/Hosting/RenderHosting.cs
src/FinSight.Api/Views/Shared/_EmailAvailability.cshtml
src/FinSight.Api/Views/Account/Login.cshtml
src/FinSight.Api/Views/Account/Register.cshtml
src/FinSight.Infrastructure/FinSight.Infrastructure.csproj
src/FinSight.Infrastructure/DependencyInjection.cs
src/FinSight.Infrastructure/Identity/DataProtectionRegistration.cs
src/FinSight.Infrastructure/Identity/EmailOptions.cs
src/FinSight.Infrastructure/Identity/EmailOptionsValidator.cs
src/FinSight.Infrastructure/Identity/SmtpEmailSender.cs
src/FinSight.Infrastructure/Persistence/FinSightDbContext.cs
src/FinSight.Infrastructure/Persistence/PostgresConfiguration.cs
src/FinSight.Infrastructure/Persistence/FinSightDesignTimeDbContextFactory.cs
src/FinSight.Infrastructure/Persistence/Migrations/20261011105742_PersistDataProtectionKeys.cs
src/FinSight.Infrastructure/Persistence/Migrations/20261011105742_PersistDataProtectionKeys.Designer.cs
src/FinSight.Infrastructure/Persistence/Migrations/FinSightDbContextModelSnapshot.cs
tests/FinSight.UnitTests/Website/AuthenticationConfigurationTests.cs
tests/FinSight.UnitTests/Website/DeploymentConfigurationTests.cs
tests/FinSight.IntegrationTests/DataProtectionPersistenceTests.cs
tools/FinSight.ContainerSmoke/FinSight.ContainerSmoke.csproj
tools/FinSight.ContainerSmoke/Program.cs
```

Added production package: `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`
10.0.12. The smoke tool reuses Testcontainers.PostgreSql 4.15.0. Earlier uncommitted
work and Domain business rules were preserved.
