# Rebaz website localization handoff

The MVC website supports `en` (default, LTR), `ar-IQ` (Arabic for Iraqi owners,
RTL), and `ku-Arab-IQ` (Badini/Kurmanji in Arabic script for Duhok, RTL).
`ku-Arab-IQ` is the selected request, cookie, and HTML `lang` tag. The Badini
strings live in the standard `ku` resource satellite so .NET 10 publishes and
loads them consistently on both Windows and the Linux Docker image. They are
**not Sorani translations**. Unsupported cultures fall back to English.

Website strings, including validation and status text, are in
`src/FinSight.Api/Resources/Website/Localization/RebazText*.resx`.
Email templates are in
`src/FinSight.Infrastructure/Resources/Identity/IdentityEmailText*.resx`.
The English resources are neutral; the email sender is not enabled or changed
by this milestone. The only new static asset is the locally served Noto Kufi
Arabic font, licensed under the bundled SIL Open Font License. Its upstream
metadata lists support for Arabic-script Kurdish:
<https://github.com/google/fonts/blob/main/ofl/notokufiarabic/METADATA.pb>.

Changing language sets the ASP.NET Core culture cookie for one year. The
selector stays in the shared navigation on public, authenticated, and narrow
screens. It posts with antiforgery protection, accepts only the three cultures,
and redirects only to known local GET pages. JavaScript warns before switching
away from a changed form. Login and logout do not clear the culture cookie.

Numeric forms accept ASCII, Arabic-Indic, and Eastern Arabic-Indic digits;
`.` and `٫` are accepted decimal marks. Commas and grouping marks are rejected
because `1,234` is ambiguous. Form values are parsed to the same .NET decimal
or integer types regardless of language. Display formatting changes with the
selected culture, but the Business currency, stored prices, simulation inputs,
and historical calculation snapshots do not. Contribution is the difference
between selling price and unit cost multiplied by quantity, **not net profit**.
There are currently no saved-simulation MVC pages; those features remain API
endpoints with unchanged JSON names, enum values, and routes.

## Translation review before customer launch

The Kurdish copy is a complete initial Badini/Kurmanji draft, **pending review
by a native Badini speaker from the Duhok audience**. In particular, review
the financial terms for contribution (`پشکا بەشداریێ`), unit cost
(`تێچوویا دانەیێ`), proposed price (`نرخێ پێشنیارکری`), recommendation,
and the below-cost warning. Confirm that the wording does not imply net profit
or a sales guarantee. Also review Arabic phrasing with an Iraqi owner.
Customer-entered business and product names are never translated.

## Manual verification

1. At desktop and phone widths, visit landing, login, registration, email
   confirmation/resend, Google onboarding, home, account, products, and preview
   in all three languages. Check alignment, form labels, table cards, arrows,
   focus rings, and the unchanged Rebaz logo.
2. Switch language while editing a form. Confirm that the warning can cancel
   navigation. Switch on a clean page, then verify the choice after navigation,
   login, and logout.
3. Create and edit a product using `12.50` and `١٢٫٥٠` as the same price.
   Confirm `1,234` is rejected, not interpreted as another value. Preview a
   calculation in each language and compare the stored-independent results.
4. Check an invalid form, an expired confirmation link, an unavailable Google
   sign-in, a below-cost warning, and unauthenticated `/api/products`.
   API callers must still receive JSON `401`/`403`, never an HTML redirect.
5. If email delivery and Google credentials are configured, inspect delivered
   emails and provider onboarding. Google-hosted consent text is controlled by
   Google, not Rebaz.

`dotnet build`, the unit tests, and a Docker image smoke test cover resource
completeness, culture selection, RTL, safe switching, numeric parsing, and
Linux rendering. The Docker smoke test should render `ku-Arab-IQ` with Badini
text and find both `/app/ku/FinSight.Api.resources.dll` and
`/app/ku/FinSight.Infrastructure.resources.dll`.
