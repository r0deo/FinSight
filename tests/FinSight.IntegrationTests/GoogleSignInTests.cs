using System.Net;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using FinSight.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.IntegrationTests;

public class GoogleSignInTests(GoogleApiFactory factory) : IClassFixture<GoogleApiFactory>
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
        Assert.Equal("/products/create", onboarding.Headers.Location!.OriginalString);
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

        // A new external round trip with the same subject and a changed email reuses the original tenant/role.
        using var again = factory.Browser();
        var repeat = await RoundTrip(again, subject, "changed@example.com", returnUrl: "https://evil.example/");
        Assert.Equal("/products", repeat.Headers.Location!.OriginalString);
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
        Assert.Equal("/products", (await RoundTrip(browser, subject, email, verified)).Headers.Location!.OriginalString);
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
        Assert.Equal("/account/google/link", (await RoundTrip(browser, subject, email, link: true)).Headers.Location!.OriginalString);
        var linked = (await users.FindByLoginAsync("Google", subject))!;
        Assert.Equal(local.Id, linked.Id);
        Assert.Equal(business, linked.BusinessId);
        Assert.Equal(new[] { "Manager" }, await users.GetRolesAsync(linked));
        using var fresh = factory.Browser();
        Assert.Equal("/products", (await RoundTrip(fresh, subject, email)).Headers.Location!.OriginalString);
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
        Assert.All(results, result => Assert.Equal("/products/create", result.Headers.Location!.OriginalString));
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
        var start = await browser.Submit("/account/google/link", []);
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
    }

    private async Task<HttpResponseMessage> RoundTrip(HttpClient browser, string subject, string email,
        bool? verified = true, bool link = false, string returnUrl = "/products")
    {
        var start = await browser.Submit(link ? "/account/google/link" : "/account/google/start",
            new() { ["ReturnUrl"] = returnUrl }, link ? "/account/google/link" : "/account/login");
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
}
