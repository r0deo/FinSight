using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using FinSight.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FinSight.IntegrationTests;

public sealed class BrowserApiFactory : FinSightApiFactory
{
    public BrowserApiFactory()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost"); san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        // Schannel on Windows requires a reimported key rather than CreateSelfSigned's ephemeral handle.
        var cert = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
        // Ephemeral test-only certificate; no production origin, credentials, or database are used.
        UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(cert)));
    }
}

public sealed partial class BrowserJourneyTests(BrowserApiFactory factory) : IClassFixture<BrowserApiFactory>
{
    [Theory]
    [InlineData("en", 1440)]
    [InlineData("en", 390)]
    [InlineData("ar-IQ", 1440)]
    [InlineData("ar-IQ", 390)]
    [InlineData("ku-Arab-IQ", 1440)]
    [InlineData("ku-Arab-IQ", 390)]
    public async Task ProductSimulationAndCosts_WorkInBrowser(string culture, int width)
    {
        var email = $"browser-{Guid.NewGuid():N}@example.com";
        Guid tenant;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            tenant = Guid.NewGuid();
            db.Businesses.Add(new Business(tenant, "Browser café", "Duhok", "IQD", email));
            await db.SaveChangesAsync();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync("Owner")) Assert.True((await roles.CreateAsync(new("Owner"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser(Guid.NewGuid(), tenant, email) { EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, "Password1!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Owner")).Succeeded);
        }
        var origin = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() {
            BaseURL = origin, IgnoreHTTPSErrors = true, ViewportSize = new() { Width = width, Height = 900 }
        });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Location + ": " + message.Text); };
        await page.GotoAsync("/account/login");
        await page.Locator("#site-language").SelectOptionAsync(culture);
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", culture);
        await page.Locator("#Email").FillAsync(email);
        await page.Locator("#Password").FillAsync("Password1!");
        await page.Locator("main form button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/account/login"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(page.Locator("form[action='/account/logout']")).ToBeVisibleAsync();
        await page.GotoAsync("/home");
        await Expect(page.Locator("header a[href='/what-if']")).ToHaveCountAsync(0);
        await Expect(page.Locator("main a[href='/what-if']")).ToHaveCountAsync(0);
        await page.GotoAsync("/products/create");
        await page.Locator("#Name").FillAsync("Browser coffee");
        await page.Locator("#Category").FillAsync("Coffee");
        await page.Locator("#SellingPrice").FillAsync(culture == "en" ? "4000" : "٤٠٠٠");
        await page.Locator("#UnitCost").FillAsync("1000");
        await page.Locator("[data-product-save]").ClickAsync();
        await page.WaitForURLAsync("**/products");
        Guid productId;
        using (var scope = factory.Services.CreateScope())
            productId = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Products
                .Where(x => x.BusinessId == tenant).Select(x => x.Id).SingleAsync();
        await page.GotoAsync($"/simulations/preview?productId={productId}");
        await page.Locator("#CurrentSalesQuantity").FillAsync("100");
        await page.Locator("#ProposedPrice").FillAsync("4500");
        await page.Locator("#CurrentSalesQuantity").FillAsync("-1");
        var invalidPricing = await page.RunAndWaitForResponseAsync(
            () => page.Locator("[data-decision-form] button[type=submit]").ClickAsync(), r => r.Request.Method == "POST" && r.Url.EndsWith("/simulations/preview"));
        Assert.Equal(400, invalidPricing.Status);
        await Expect(page.Locator("#CurrentSalesQuantity")).ToBeFocusedAsync();
        await Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await page.Locator("#CurrentSalesQuantity").FillAsync("100");
        await page.Locator("[data-decision-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-decision-results]")).ToBeVisibleAsync();
        await InspectResultModal(page, "pricing-result", "[data-decision-form]", "[data-decision-form] button[type=submit]", culture, width);
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AnyAsync(x => x.BusinessId == tenant));
        await page.Locator("#ProposedPrice").FillAsync("4600");
        await Expect(page.Locator("[data-result-open='pricing-result']")).ToBeHiddenAsync();
        await page.Locator("[data-decision-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("dialog[open] [name=ProposedPrice]")).ToHaveValueAsync("4600");
        await page.Locator("[data-decision-save] button").ClickAsync();
        await page.WaitForURLAsync(url => System.Text.RegularExpressions.Regex.IsMatch(url, @"/simulations/[a-f0-9-]{36}$"));
        await Layout(page, culture, width, "pricing");
        await Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await page.GotoAsync($"/simulations/preview?productId={productId}");
        await page.Locator($"a[href='/what-if?productId={productId}']").ClickAsync();
        await Expect(page.Locator("#UsePriceResponse")).ToHaveCountAsync(0);
        await Expect(page.Locator("[data-demand-assumption]")).ToBeHiddenAsync();
        await page.Locator("#Concern").SelectOptionAsync("Both");
        await page.Locator("#CurrentQuantity").FillAsync("100");
        await page.Locator("[data-preset=cost]").ClickAsync();
        await page.Locator("[data-preset=demand]").ClickAsync();
        await page.Locator("#CurrentQuantity").FillAsync("-1");
        var invalidWhatIf = await page.RunAndWaitForResponseAsync(
            () => page.Locator("[data-what-if-form] button[type=submit]").ClickAsync(), r => r.Request.Method == "POST" && r.Url.EndsWith("/what-if/preview"));
        Assert.Equal(400, invalidWhatIf.Status);
        await Expect(page.Locator("#CurrentQuantity")).ToBeFocusedAsync();
        await Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await page.Locator("#CurrentQuantity").FillAsync("100");
        await page.Locator("[data-what-if-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-what-if-results]").First).ToBeVisibleAsync();
        await Layout(page, culture, width, "shock");
        await InspectResultModal(page, "what-if-result", "[data-what-if-form]", "[data-what-if-form] button[type=submit]", culture, width);
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().WhatIfSimulations.AnyAsync(x => x.BusinessId == tenant));
        await page.Locator("#UsePriceResponse").CheckAsync();
        await Expect(page.Locator("#AlternativePrice")).ToBeVisibleAsync();
        await page.Locator("#AlternativePrice").FillAsync("4500");
        await Expect(page.Locator("#AlternativePriceQuantity")).ToHaveValueAsync("90");
        await Expect(page.Locator("[data-what-if-save]")).ToBeHiddenAsync();
        await Expect(page.Locator("[data-result-open='what-if-result']")).ToBeHiddenAsync();
        await page.Locator("[data-what-if-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-what-if-save]")).ToBeVisibleAsync();
        await Layout(page, culture, width, "responses");
        var saveRequests = 0;
        page.Request += (_, request) => { if (request.Method == "POST" && request.Url.EndsWith("/what-if/save")) saveRequests++; };
        await page.Locator("[data-what-if-save] button").ClickAsync(new() { ClickCount = 2 });
        await page.WaitForURLAsync(url => System.Text.RegularExpressions.Regex.IsMatch(url, @"/what-if/[a-f0-9-]{36}$"));
        await Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        Assert.Equal(1, saveRequests);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var snapshot = await db.WhatIfSimulations.SingleAsync(x => x.BusinessId == tenant);
            Assert.Equal(300000, snapshot.Result.BaselineContribution);
            Assert.Equal(252000, snapshot.Result.NoActionContribution);
            Assert.Equal(297000, snapshot.Result.Options[1].Contribution);
            Assert.Equal(91, snapshot.Result.Options[1].MinimumUnitsToMaintainBaseline);
            Assert.Equal(4000, (await db.Products.SingleAsync(x => x.Id == productId)).SellingPrice);
            Assert.Single(await db.PricingSimulations.Where(x => x.BusinessId == tenant).ToListAsync());
        }
        await page.Locator("header a[href='/business-costs']").ClickAsync();
        await page.WaitForURLAsync("**/business-costs");
        await page.Locator("#month").FillAsync("2026-10");
        await page.Locator("main form[method=get] button").ClickAsync();
        await page.WaitForURLAsync("**/business-costs?month=2026-10");
        await Layout(page, culture, width, "costs-empty");
        await page.Locator("main a[href='/business-costs/create?month=2026-10']").ClickAsync();
        await page.Locator("#Name").FillAsync("Kitchen rent");
        await page.Locator("#Category").SelectOptionAsync("Rent");
        await page.Locator("#Amount").FillAsync(culture == "en" ? "125.50" : "١٢٥٫٥٠");
        await page.Locator("#Inclusion").SelectOptionAsync("No");
        await page.Locator("main form button[type=submit]").ClickAsync();
        await page.WaitForURLAsync("**/business-costs?month=2026-10");
        await page.Locator("main a[href$='/edit']").ClickAsync();
        await page.Locator("#Amount").FillAsync("150");
        await page.Locator("main form button[type=submit]").ClickAsync();
        await page.WaitForURLAsync("**/business-costs?month=2026-10");
        await Layout(page, culture, width, "costs");
        await page.Locator("[data-cost-review] button").ClickAsync();
        await page.WaitForURLAsync("**/business-costs?month=2026-10");
        await Expect(page.Locator("[data-cost-review] input[name=reviewed]")).ToHaveValueAsync("false");
        await page.Locator("#Percent").FillAsync(culture == "en" ? "10" : "١٠");
        await page.Locator("[data-cost-adjustment] button").ClickAsync();
        var numberCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
        await Expect(page.Locator("[data-cost-adjustment-result]")).ToContainTextAsync(15m.ToString("N2", numberCulture));
        await Expect(page.Locator("[data-cost-adjustment-result]")).ToContainTextAsync(135m.ToString("N2", numberCulture));
        await Layout(page, culture, width, "cost-adjustment");
        await page.Locator("[data-cost-adjustment-result]").ScrollIntoViewIfNeededAsync();
        await page.ScreenshotAsync(new() { Path = Path.Combine(AppContext.BaseDirectory, "TestResults", "browser", $"{culture}-{width}-cost-adjustment-detail.png") });
        await InspectResultModal(page, "cost-adjustment-result", "[data-cost-adjustment]", "[data-cost-adjustment] button[type=submit]", culture, width);
        await page.Locator("#Percent").FillAsync("20");
        await Expect(page.Locator("[data-result-open='cost-adjustment-result']")).ToBeHiddenAsync();
        await page.Locator("[data-cost-adjustment] button").ClickAsync();
        await Expect(page.Locator("dialog[open] [data-cost-adjustment-result]")).ToContainTextAsync(30m.ToString("N2", numberCulture));
        await Expect(page.Locator("dialog[open] [data-cost-adjustment-result]")).ToContainTextAsync(120m.ToString("N2", numberCulture));
        await page.Keyboard.PressAsync("Escape");
        await page.Locator("#Percent").FillAsync("-1");
        var invalidAdjustment = await page.RunAndWaitForResponseAsync(
            () => page.Locator("[data-cost-adjustment] button").ClickAsync(), r => r.Request.Method == "POST" && r.Url.EndsWith("/business-costs/adjustment"));
        Assert.Equal(400, invalidAdjustment.Status);
        await Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await Expect(page.Locator("#Percent")).ToBeFocusedAsync();
        await Expect(page.Locator("#adjustment-percent-error")).ToBeVisibleAsync();
        // Switching language from a POST result returns safely to the same month, never to a POST-only URL.
        await page.Locator("#site-language").SelectOptionAsync(culture == "en" ? "ar-IQ" : "en");
        await page.WaitForURLAsync("**/business-costs?month=2026-10");
        await page.Locator("#site-language").SelectOptionAsync(culture);
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", culture);
        // Month navigation must still work after the read-only POST preview.
        await page.Locator("#month").FillAsync("2026-11");
        await page.Locator("main form[method=get] button").ClickAsync();
        await page.WaitForURLAsync("**/business-costs?month=2026-11");
        await Expect(page.Locator("main a[href$='/edit']")).ToHaveCountAsync(0);
        await page.GotoAsync("/business-costs?month=2026-10");
        using (var scope = factory.Services.CreateScope())
        {
            var entry = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().BusinessCosts.SingleAsync(x => x.BusinessId == tenant);
            Assert.Equal(150, entry.Amount); Assert.Equal(2, entry.Version);
            Assert.Equal(new DateOnly(2026,10,1), entry.Month);
            Assert.NotNull((await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().BusinessCostMonths.SingleAsync(x => x.BusinessId == tenant)).ReviewedAtUtc);
        }
        Assert.All(errors, error => Assert.True(error.Contains("Failed to load resource") && error.Contains("400"), error));
        if (culture == "en" && width == 1440)
        {
            // Failure paths use the real server, not a mocked success/error response.
            await page.GotoAsync("/business-costs/create?month=2026-10");
            await page.Locator("#Name").FillAsync("Rejected cost");
            await page.Locator("#Amount").FillAsync("-1");
            var invalid = await page.RunAndWaitForResponseAsync(
                () => page.Locator("main form button[type=submit]").ClickAsync(),
                r => r.Request.Method == "POST" && r.Url.EndsWith("/business-costs/create"));
            Assert.Equal(400, invalid.Status);
            await Expect(page.Locator(".validation-summary-errors")).ToBeVisibleAsync();
            await Expect(page.Locator("#Name")).ToHaveValueAsync("Rejected cost");

            await page.GotoAsync($"/what-if?productId={productId}");
            await page.Locator("#CurrentQuantity").FillAsync("100");
            await page.Locator("#CostIncreasePercent").FillAsync("20");
            await page.Locator("[data-what-if-form] button[type=submit]").ClickAsync();
            await Expect(page.Locator("[data-what-if-save]")).ToBeVisibleAsync();
            await page.Locator("[name=PreviewProof]").EvaluateAsync("input => input.value = 'invalid-proof'");
            var rejected = await page.RunAndWaitForResponseAsync(
                () => page.Locator("[data-what-if-save] button").ClickAsync(),
                r => r.Request.Method == "POST" && r.Url.EndsWith("/what-if/save"));
            Assert.Equal(409, rejected.Status);
            await Expect(page.Locator("main")).ToContainTextAsync("Recalculate the preview");
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            Assert.Equal(1, await db.BusinessCosts.CountAsync(x => x.BusinessId == tenant));
            Assert.Equal(1, await db.WhatIfSimulations.CountAsync(x => x.BusinessId == tenant));
            // Chromium reports expected HTTP error responses in its console. No script/CSP errors are allowed.
            Assert.All(errors, error => Assert.True(error.Contains("Failed to load resource") &&
                (error.Contains("400") || error.Contains("409")), error));
        }
        await page.GotoAsync("/business-costs?month=2026-10");
        await page.Locator("main a[href$='/delete']").ClickAsync();
        await Layout(page, culture, width, "cost-delete-confirmation");
        await page.Locator("main form button[type=submit]").ClickAsync();
        await page.WaitForURLAsync("**/business-costs?month=2026-10");
        await Expect(page.Locator("main a[href$='/edit']")).ToHaveCountAsync(0);
        await Expect(page.Locator("[data-cost-review]")).ToHaveCountAsync(0);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            Assert.False(await db.BusinessCosts.AnyAsync(x => x.BusinessId == tenant));
            Assert.Null((await db.BusinessCostMonths.SingleAsync(x => x.BusinessId == tenant)).ReviewedAtUtc);
            Assert.Equal(1, await db.PricingSimulations.CountAsync(x => x.BusinessId == tenant));
            Assert.Equal(1, await db.WhatIfSimulations.CountAsync(x => x.BusinessId == tenant));
        }
    }
    private static async Task Layout(IPage page, string culture, int width, string step)
    {
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", culture);
        await Expect(page.Locator("html")).ToHaveAttributeAsync("dir", culture == "en" ? "ltr" : "rtl");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"), $"Overflow: {culture} {width} {step}");
        var text = await page.Locator("main").InnerTextAsync();
        Assert.DoesNotContain("ToString(", text); Assert.DoesNotContain("CultureInfo", text);
        var folder = Path.Combine(AppContext.BaseDirectory, "TestResults", "browser");
        Directory.CreateDirectory(folder);
        await page.ScreenshotAsync(new() { Path = Path.Combine(folder, $"{culture}-{width}-{step}.png"),
            FullPage = true, Caret = ScreenshotCaret.Initial });
        if (await page.Locator(".decision-answer").CountAsync() > 0)
        {
            await page.Locator(".decision-answer").ScrollIntoViewIfNeededAsync();
            await page.ScreenshotAsync(new() { Path = Path.Combine(folder, $"{culture}-{width}-{step}-detail.png"),
                Caret = ScreenshotCaret.Initial });
        }
    }
}
