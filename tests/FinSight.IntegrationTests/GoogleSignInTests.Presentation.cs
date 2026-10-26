using System.Net;
using FinSight.IntegrationTests.Infrastructure;

namespace FinSight.IntegrationTests;

public partial class GoogleSignInTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ar-IQ")]
    [InlineData("ku-Arab-IQ")]
    public async Task GoogleOnlyPages_PreserveOnboardingAndExistingUserDestination(string culture)
    {
        using var browser = factory.Browser();
        browser.DefaultRequestHeaders.AcceptLanguage.ParseAdd(culture);
        foreach (var path in new[] { "/account/login", "/account/register" })
        {
            var html = await browser.GetStringAsync(path);
            Assert.Contains("action=\"/account/google/start\"", html);
            foreach (var field in new[] { "Email", "Password", "ConfirmPassword", "BusinessName" })
                Assert.DoesNotContain($"name=\"{field}\"", html);
            Assert.DoesNotContain($"action=\"{path}\"", html);
            Assert.DoesNotContain("href=\"/account/resend-confirmation\"", html);
            Assert.DoesNotContain("Use your confirmed business account.", html);
            Assert.DoesNotContain("Email registration also requires", html);
        }
        var landing = await browser.GetStringAsync("/");
        Assert.DoesNotContain("getting-started", landing);
        Assert.DoesNotContain("class=\"steps\"", landing);
        var subject = Guid.NewGuid().ToString();
        var email = $"ui-google-{Guid.NewGuid():N}@example.com";
        var signup = await RoundTrip(browser, subject, email, formPage: "/account/register");
        Assert.Equal("/account/google/business", signup.Headers.Location!.OriginalString);
        var onboard = await browser.Submit("/account/google/business", WebsiteTestClient.Registration(email));
        Assert.Equal("/home", onboard.Headers.Location!.OriginalString);
        var home = await browser.GetStringAsync("/home");
        Assert.DoesNotContain("getting-started", home);
        Assert.Contains("href=\"/products/create\"", home);
        await browser.Submit("/account/logout", [], "/home");
        var login = await RoundTrip(browser, subject, email, returnUrl: "/products", formPage: "/account/login?returnUrl=%2Fproducts");
        Assert.Equal("/products", login.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/products")).StatusCode);
    }
}
