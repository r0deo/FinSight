using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using static Microsoft.Playwright.Assertions;

namespace FinSight.IntegrationTests;

public sealed partial class BrowserJourneyTests
{
    // Existing product/simulation journeys use the preserved password endpoint for
    // fixture sign-in, not removed UI controls. Real cookies and antiforgery still apply.
    private static async Task SignInForJourney(IPage page, string email)
    {
        var token = await page.Locator("input[name=__RequestVerificationToken]").First.InputValueAsync();
        using var fields = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = "Password1!", ["ReturnUrl"] = "/home",
            ["__RequestVerificationToken"] = token
        });
        var response = await page.Context.APIRequest.PostAsync("/account/login", new()
        {
            Data = await fields.ReadAsStringAsync(),
            Headers = new Dictionary<string, string> { ["Content-Type"] = "application/x-www-form-urlencoded" }
        });
        Assert.Equal(200, response.Status);
        await page.GotoAsync("/home");
        await Expect(page.Locator("form[action='/account/logout']")).ToBeVisibleAsync();
    }
}

// Public views and the outbound OAuth challenge do not access a database. Keep
// these UI checks runnable even when Docker is unavailable. Onboarding/callback
// persistence continues to be covered by GoogleSignInTests against PostgreSQL.
public sealed class PublicPagesFactory : WebApplicationFactory<Program>
{
    public string Origin { get; }
    public PublicPagesFactory()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Origin = $"https://localhost:{port}";
        listener.Stop();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        var cert = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
        UseKestrel(options => options.Listen(IPAddress.Loopback, port, listen => listen.UseHttps(cert)));
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // These flags must be available while Program registers authentication schemes.
        builder.UseSetting("Authentication:Google:Enabled", "true");
        builder.UseSetting("Authentication:Google:ClientId", "test-client");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-secret-not-real");
        builder.UseSetting("RateLimiting:AuthenticationPermitLimit", "1000");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=1;Database=unused_ui_test;Username=unused;Password=unused",
            ["Jwt:SigningKey"] = new string('x', 64),
            ["Email:Enabled"] = "false",
            ["Email:WebsiteBaseUrl"] = Origin,
            ["Authentication:Google:Enabled"] = "true",
            ["Authentication:Google:ClientId"] = "test-client",
            ["Authentication:Google:ClientSecret"] = "test-secret-not-real",
            ["RateLimiting:AuthenticationPermitLimit"] = "1000"
        }));
        builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
    }
}

public sealed class PublicPagesBrowserTests(PublicPagesFactory factory) : IClassFixture<PublicPagesFactory>
{
    [Theory]
    [InlineData("en", 1440)]
    [InlineData("en", 390)]
    [InlineData("ar-IQ", 1440)]
    [InlineData("ar-IQ", 390)]
    [InlineData("ku-Arab-IQ", 1440)]
    [InlineData("ku-Arab-IQ", 390)]
    public async Task PublicPages_AreCenteredAndGoogleOnly(string culture, int width)
    {
        _ = factory.Services; // Start the test-only Kestrel host.
        var origin = factory.Origin;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new()
        {
            BaseURL = origin, IgnoreHTTPSErrors = true, ViewportSize = new() { Width = width, Height = 900 }
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync("/");
        await page.Locator("#site-language").SelectOptionAsync(culture);
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", culture);
        await Expect(page.Locator(".getting-started, .steps")).ToHaveCountAsync(0);
        await Expect(page.Locator(".hero a[href='/account/register']")).ToBeVisibleAsync();
        await Expect(page.Locator(".hero a[href='/account/login']")).ToBeVisibleAsync();
        Assert.Equal("center", await page.Locator(".hero").EvaluateAsync<string>("e => getComputedStyle(e).textAlign"));
        var hero = (await page.Locator(".hero").BoundingBoxAsync())!;
        Assert.InRange(Math.Abs(hero.X + hero.Width / 2 - width / 2f), 0, 2);
        await Layout(page, culture, width, "landing-simplified");

        // Submit to the real handler, but stop its redirect before any Google request.
        // Intercepting only the destination is insufficient for a redirected POST.
        IAPIResponse? challenge = null;
        await page.RouteAsync("**/account/google/start", async route =>
        {
            challenge = await route.FetchAsync(new() { MaxRedirects = 0 });
            await route.FulfillAsync(new()
            {
                Status = 200, ContentType = "text/html", Body = "<p>Test-only Google authorization boundary</p>"
            });
        });
        foreach (var path in new[] { "/account/login", "/account/register" })
        {
            await page.GotoAsync(path + "?returnUrl=%2Fproducts");
            await Expect(page.Locator("main input:not([type=hidden])")).ToHaveCountAsync(0);
            await Expect(page.Locator("main form[action='/account/login'], main form[action='/account/register']")).ToHaveCountAsync(0);
            await Expect(page.Locator("main a[href*='confirmation'], main a[href*='password']")).ToHaveCountAsync(0);
            var button = page.Locator("main .google-signin button");
            await Expect(button).ToBeVisibleAsync();
            await Expect(page.Locator(".google-signin input[name=returnUrl]")).ToHaveValueAsync("/products");
            await button.FocusAsync();
            await Expect(button).ToBeFocusedAsync();
            await Layout(page, culture, width, path.EndsWith("login") ? "login-google-only" : "register-google-only");
            challenge = null;
            await button.ClickAsync();
            await Expect(page.Locator("p")).ToHaveTextAsync("Test-only Google authorization boundary");
            Assert.NotNull(challenge);
            Assert.Equal(302, challenge.Status);
            Assert.StartsWith("https://accounts.google.com/", challenge.Headers["location"]);
            Assert.Contains("redirect_uri=", challenge.Headers["location"]);
            Assert.Contains("state=", challenge.Headers["location"]);
        }
    }

    private static async Task Layout(IPage page, string culture, int width, string step)
    {
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", culture);
        await Expect(page.Locator("html")).ToHaveAttributeAsync("dir", culture == "en" ? "ltr" : "rtl");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= window.innerWidth + 1"));
        var folder = Path.Combine(AppContext.BaseDirectory, "TestResults", "browser");
        Directory.CreateDirectory(folder);
        await page.ScreenshotAsync(new() { Path = Path.Combine(folder, $"{culture}-{width}-{step}.png"), FullPage = true });
    }
}
