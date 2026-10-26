using System.Net;
using System.Net.Http.Json;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using FinSight.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FinSight.IntegrationTests;

public partial class GoogleSignInTests(GoogleApiFactory factory) : IClassFixture<GoogleApiFactory>
{
    [Fact]
    public async Task VerifiedGoogleIdentity_MustOnboardBeforeTenantAccess_AndRetriesDoNotDuplicateAccounts()
    {
        using var browser = factory.Browser();
        var email = $"google-{Guid.NewGuid():N}@example.com";
        var subject = Guid.NewGuid().ToString();
        var callback = await RoundTrip(browser, subject, email);
        Assert.Equal("/account/google/business", callback.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/products")).StatusCode);
        var fields = WebsiteTestClient.Registration(email);
        fields["Email"] = "attacker@example.com";
        fields["BusinessId"] = Guid.NewGuid().ToString();
        fields["Role"] = "Manager";
        var onboarding = await browser.Submit("/account/google/business", fields);
        Assert.Equal("/home", onboarding.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/products")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByLoginAsync("Google", subject))!;
        Assert.Equal(email, user.Email);
        Assert.True(user.EmailConfirmed);
        Assert.Equal(new[] { "Owner" }, await users.GetRolesAsync(user));
        Assert.NotEqual(fields["BusinessId"], user.BusinessId.ToString());
        Assert.False(await users.HasPasswordAsync(user));
        var settings = await browser.GetStringAsync("/account/settings");
        Assert.Contains("Google connected", settings);
        Assert.DoesNotContain(">Connect Google<", settings);
        var home = await browser.GetStringAsync("/home");
        Assert.Contains("Add your first product", home);
        Assert.DoesNotContain("Link Google", home);
        Assert.DoesNotContain(">Connect Google<", home);
        foreach (var path in new[] { "/account/register", "/account/login", "/account/google/business" })
            Assert.Equal("/home", (await browser.GetAsync(path)).Headers.Location!.OriginalString);
        // Duplicate callback after a completed signup is harmless, not an error/re-onboarding loop.
        Assert.Equal("/home", (await browser.GetAsync("/account/google/callback")).Headers.Location!.OriginalString);

        // A new external round trip with the same subject and a changed email reuses the original tenant/role.
        using var again = factory.Browser();
        var repeat = await RoundTrip(again, subject, "changed@example.com", returnUrl: "https://evil.example/");
        Assert.Equal("/home", repeat.Headers.Location!.OriginalString);
        Assert.Equal(user.Id, (await users.FindByLoginAsync("Google", subject))!.Id);
        Assert.Single(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Users.Where(u => u.Email == email).ToListAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await again.GetAsync("/account/google/business")).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task UnverifiedOrMissingProviderVerification_RequiresEmailConfirmation(bool? verified)
    {
        using var browser = factory.Browser();
        var email = $"unverified-{Guid.NewGuid():N}@example.com";
        var subject = Guid.NewGuid().ToString();
        await RoundTrip(browser, subject, email, verified);
        var result = await browser.Submit("/account/google/business", WebsiteTestClient.Registration(email));
        Assert.Equal("/account/check-email", result.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/products")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByLoginAsync("Google", subject))!.EmailConfirmed);
        await browser.Confirm(factory.EmailSender.Messages.Last(m => m.Recipient == email));
        Assert.Equal("/home", (await RoundTrip(browser, subject, email, verified)).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task MatchingLocalEmail_IsNotLinked_AuthenticatedExplicitLinkPreservesTenantAndRole()
    {
        using var browser = factory.Browser();
        var email = $"local-{Guid.NewGuid():N}@example.com";
        var subject = Guid.NewGuid().ToString();
        await browser.Submit("/account/register", WebsiteTestClient.Registration(email));
        await browser.Confirm(factory.EmailSender.Messages.Last(m => m.Recipient == email));
        var callback = await RoundTrip(browser, subject, email);
        Assert.Equal("/account/google/existing-account", callback.Headers.Location!.OriginalString);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByLoginAsync("Google", subject));
        var local = (await users.FindByEmailAsync(email))!;
        var business = local.BusinessId;
        Assert.True((await users.RemoveFromRoleAsync(local, "Owner")).Succeeded);
        // Registration provisions both supported roles.
        Assert.True((await users.AddToRoleAsync(local, "Manager")).Succeeded);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Submit("/account/login", new() { ["Email"] = email, ["Password"] = "Password1!" })).StatusCode);
        Assert.Contains(">Connect Google<", await browser.GetStringAsync("/account/settings"));
        Assert.DoesNotContain(">Connect Google<", await browser.GetStringAsync("/home"));
        Assert.Equal("/account/settings", (await RoundTrip(browser, subject, email, link: true)).Headers.Location!.OriginalString);
        var linked = (await users.FindByLoginAsync("Google", subject))!;
        Assert.Equal(local.Id, linked.Id);
        Assert.Equal(business, linked.BusinessId);
        Assert.Equal(new[] { "Manager" }, await users.GetRolesAsync(linked));
        Assert.Equal("/account/settings", (await RoundTrip(browser, subject, email, link: true)).Headers.Location!.OriginalString);
        // Link state survives a completely separate password-authenticated session.
        using var passwordSession = factory.Browser();
        await passwordSession.Submit("/account/login", new() { ["Email"] = email, ["Password"] = "Password1!" });
        var settings = await passwordSession.GetStringAsync("/account/settings");
        Assert.Contains("Google connected", settings);
        Assert.DoesNotContain(">Connect Google<", settings);
        using var fresh = factory.Browser();
        Assert.Equal("/home", (await RoundTrip(fresh, subject, email)).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ConcurrentOnboardingForSameProviderSubject_CreatesExactlyOneBusinessAndOwner()
    {
        using var first = factory.Browser();
        using var second = factory.Browser();
        var email = $"retry-{Guid.NewGuid():N}@example.com";
        var subject = Guid.NewGuid().ToString();
        await RoundTrip(first, subject, email);
        await RoundTrip(second, subject, email);
        var fields = WebsiteTestClient.Registration(email);
        fields["BusinessName"] = "Concurrent-" + subject;
        var results = await Task.WhenAll(first.Submit("/account/google/business", new(fields)),
            second.Submit("/account/google/business", new(fields)));
        Assert.All(results, result => Assert.Equal("/home", result.Headers.Location!.OriginalString));
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
        Assert.Equal(1, await database.Businesses.CountAsync(b => b.Name == fields["BusinessName"]));
        Assert.Equal(1, await database.Users.CountAsync(u => u.Email == email));
        Assert.Equal(1, await database.UserLogins.CountAsync(l => l.LoginProvider == "Google" && l.ProviderKey == subject));
    }

    [Fact]
    public async Task LinkCallback_IsBoundToTheAuthenticatedUserWhoStartedIt()
    {
        using var browser = factory.Browser();
        var emails = new[] { $"link-a-{Guid.NewGuid():N}@example.com", $"link-b-{Guid.NewGuid():N}@example.com" };
        foreach (var email in emails)
        {
            await browser.Submit("/account/register", WebsiteTestClient.Registration(email));
            await browser.Confirm(factory.EmailSender.Messages.Last(m => m.Recipient == email));
        }
        await browser.Submit("/account/login", new() { ["Email"] = emails[0], ["Password"] = "Password1!" });
        var start = await browser.Submit("/account/google/link", [], "/account/settings");
        var state = QueryHelpers.ParseQuery(start.Headers.Location!.Query)["state"].ToString();
        await browser.Submit("/account/logout", [], "/products");
        await browser.Submit("/account/login", new() { ["Email"] = emails[1], ["Password"] = "Password1!" });
        var subject = Guid.NewGuid().ToString();
        var code = factory.Backchannel.Add(subject, emails[0]);
        var middleware = await browser.GetAsync("/signin-google?code=" + code + "&state=" + Uri.EscapeDataString(state));
        var result = await browser.GetAsync(middleware.Headers.Location);
        Assert.Equal("/account/google/error", result.Headers.Location!.OriginalString);
        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByLoginAsync("Google", subject));
    }

    [Fact]
    public async Task MissingStateCancellationAndForgery_FailCleanly()
    {
        using var browser = factory.Browser();
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/account/google/start", new FormUrlEncodedContent([]))).StatusCode);
        var invalid = await browser.GetAsync("/signin-google?code=forged&state=invalid");
        Assert.Equal("/account/google/error", invalid.Headers.Location!.OriginalString);
        var start = await browser.Submit("/account/google/start", [], "/account/login");
        var query = QueryHelpers.ParseQuery(start.Headers.Location!.Query);
        var cancel = await browser.GetAsync("/signin-google?error=access_denied&state=" + Uri.EscapeDataString(query["state"].ToString()));
        Assert.Equal("/account/google/error", cancel.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/account/google/error")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/account/google/link-callback")).StatusCode);
        Assert.Equal("/account/google/error", (await browser.GetAsync("/account/google/callback")).Headers.Location!.OriginalString);
        Assert.Equal("/account/google/error", (await browser.GetAsync("/account/google/business")).Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task IncompleteOnboarding_CanRestartWithoutPartialAccounts_AndCannotAccessHome()
    {
        var email = $"resume-{Guid.NewGuid():N}@example.com";
        var subject = Guid.NewGuid().ToString();
        using var abandoned = factory.Browser();
        await RoundTrip(abandoned, subject, email);
        Assert.Contains("/account/login", (await abandoned.GetAsync("/home")).Headers.Location!.OriginalString);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByLoginAsync("Google", subject));
        Assert.Null(await users.FindByEmailAsync(email));
        using var resumed = factory.Browser();
        Assert.Equal("/account/google/business", (await RoundTrip(resumed, subject, email)).Headers.Location!.OriginalString);
        Assert.Equal("/home", (await resumed.Submit("/account/google/business", WebsiteTestClient.Registration(email))).Headers.Location!.OriginalString);
        Assert.NotNull(await users.FindByLoginAsync("Google", subject));
    }

    [Fact]
    public async Task GoogleIdentityLinkedToAnotherUser_CannotBeTakenOver()
    {
        using var owner = factory.Browser();
        using var other = factory.Browser();
        var subject = Guid.NewGuid().ToString();
        var email = $"linked-{Guid.NewGuid():N}@example.com";
        await RoundTrip(owner, subject, email);
        await owner.Submit("/account/google/business", WebsiteTestClient.Registration(email));
        var otherEmail = $"other-{Guid.NewGuid():N}@example.com";
        await other.Submit("/account/register", WebsiteTestClient.Registration(otherEmail));
        await other.Confirm(factory.EmailSender.Messages.Last(m => m.Recipient == otherEmail));
        await other.Submit("/account/login", new() { ["Email"] = otherEmail, ["Password"] = "Password1!" });
        Assert.Equal("/account/google/error", (await RoundTrip(other, subject, email, link: true)).Headers.Location!.OriginalString);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(email, (await users.FindByLoginAsync("Google", subject))!.Email);
        Assert.Empty(await users.GetLoginsAsync((await users.FindByEmailAsync(otherEmail))!));
    }

    private async Task<HttpResponseMessage> RoundTrip(HttpClient browser, string subject, string email,
        bool? verified = true, bool link = false, string returnUrl = "/home", string? formPage = null)
    {
        var start = await browser.Submit(link ? "/account/google/link" : "/account/google/start",
            new() { ["ReturnUrl"] = returnUrl }, formPage ?? (link ? "/account/settings" : "/account/login"));
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var query = QueryHelpers.ParseQuery(start.Headers.Location!.Query);
        Assert.Equal("https://accounts.google.com", start.Headers.Location.GetLeftPart(UriPartial.Authority));
        Assert.Equal("https://localhost:7080/signin-google", query["redirect_uri"].ToString());
        Assert.Equal("openid email profile", query["scope"].ToString());
        var code = factory.Backchannel.Add(subject, email, verified);
        var middleware = await browser.GetAsync("/signin-google?code=" + code + "&state=" + Uri.EscapeDataString(query["state"].ToString()));
        Assert.Equal(HttpStatusCode.Redirect, middleware.StatusCode);
        return await browser.GetAsync(middleware.Headers.Location);
    }

    [Fact]
    public async Task DisabledEmail_BlocksPasswordSignup_ButAllowsConfirmedPasswordAndVerifiedGoogle()
    {
        using var browser = factory.Browser();
        var email = $"eligible-{Guid.NewGuid():N}@example.com";
        await browser.Submit("/account/register", WebsiteTestClient.Registration(email));
        await browser.Confirm(factory.EmailSender.Messages.Last(m => m.Recipient == email));
        using var scope = factory.Services.CreateScope();
        using var disabledHost = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Email:Enabled"] = "false" })));
        using var disabled = disabledHost.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost:7080"), AllowAutoRedirect = false, HandleCookies = true });
        {
            var signup = await disabled.GetStringAsync("/account/register");
            Assert.DoesNotContain("Email registration and confirmation delivery", signup);
            Assert.Contains("Continue with Google", signup);
            Assert.DoesNotContain("name=\"Password\"", signup);
            var blockedEmail = $"blocked-{Guid.NewGuid():N}@example.com";
            var blocked = await disabled.Submit("/account/register", WebsiteTestClient.Registration(blockedEmail), "/account/login");
            Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
            var apiBlocked = await disabled.PostAsJsonAsync("/api/auth/register", new
            {
                Email = blockedEmail, Password = "Password1!", BusinessName = "Blocked Cafe",
                Location = "Baghdad", Currency = "IQD", ContactInformation = blockedEmail
            });
            Assert.Equal(HttpStatusCode.BadRequest, apiBlocked.StatusCode);
            Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(blockedEmail));
            Assert.DoesNotContain("Send confirmation email", await disabled.GetStringAsync("/account/resend-confirmation"));
            Assert.Equal("/home", (await disabled.Submit("/account/login", new() { ["Email"] = email, ["Password"] = "Password1!" })).Headers.Location!.OriginalString);
            using var google = disabledHost.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost:7080"), AllowAutoRedirect = false, HandleCookies = true });
            var googleEmail = $"pilot-{Guid.NewGuid():N}@example.com";
            await RoundTrip(google, Guid.NewGuid().ToString(), googleEmail);
            Assert.Equal("/home", (await google.Submit("/account/google/business", WebsiteTestClient.Registration(googleEmail))).Headers.Location!.OriginalString);
            Assert.DoesNotContain(factory.EmailSender.Messages, m => m.Recipient == googleEmail);
        }
    }

    [Fact]
    public async Task ExpiredExternalAndApplicationSessions_OfferSafeRetryWithoutTenantAccess()
    {
        var clock = new MutableTimeProvider();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ExternalScheme, options => options.TimeProvider = clock);
            services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options => options.TimeProvider = clock);
        }));
        using var browser = host.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost:7080"), AllowAutoRedirect = false, HandleCookies = true });
        var email = $"expires-{Guid.NewGuid():N}@example.com";
        var subject = Guid.NewGuid().ToString();
        await RoundTrip(browser, subject, email);
        clock.SetUtcNow(DateTimeOffset.UtcNow.AddMinutes(31));
        Assert.Equal("/account/google/error", (await browser.GetAsync("/account/google/business")).Headers.Location!.OriginalString);
        Assert.Contains("/account/login", (await browser.GetAsync("/home")).Headers.Location!.OriginalString);
        clock.SetUtcNow(DateTimeOffset.UtcNow);
        await RoundTrip(browser, subject, email);
        Assert.Equal("/home", (await browser.Submit("/account/google/business", WebsiteTestClient.Registration(email))).Headers.Location!.OriginalString);
        clock.SetUtcNow(DateTimeOffset.UtcNow.AddHours(9));
        var expired = await browser.GetAsync("/home");
        Assert.Contains("/account/login", expired.Headers.Location!.OriginalString);
        Assert.Contains("session may have expired", await browser.GetStringAsync(expired.Headers.Location));
    }
}
