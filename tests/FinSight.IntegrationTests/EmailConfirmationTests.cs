using System.Net;
using System.Net.Http.Json;
using FinSight.Infrastructure.Identity;
using FinSight.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinSight.IntegrationTests;

public class EmailConfirmationTests(FinSightApiFactory factory) : IClassFixture<FinSightApiFactory>
{
    [Fact]
    public async Task UnconfirmedPasswordAccount_IsRejectedOnBothSurfaces_ThenValidConfirmationAllowsLogin()
    {
        using var browser = factory.Browser();
        var email = $"confirm-{Guid.NewGuid():N}@example.com";
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Submit("/account/register", WebsiteTestClient.Registration(email))).StatusCode);
        var fields = new Dictionary<string, string> { ["Email"] = email, ["Password"] = "Password1!" };
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.Submit("/account/login", fields)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Password1!" })).StatusCode);
        await browser.Confirm(factory.EmailSender.Messages.Last(m => m.Recipient == email));
        Assert.Equal(HttpStatusCode.Redirect, (await browser.Submit("/account/login", fields)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Password1!" })).StatusCode);
    }

    [Fact]
    public async Task InvalidAndExpiredConfirmationTokens_AreRejectedWithoutConfirmingAccount()
    {
        using var browser = factory.Browser();
        var email = $"invalid-{Guid.NewGuid():N}@example.com";
        await browser.Submit("/account/register", WebsiteTestClient.Registration(email));
        var message = factory.EmailSender.Messages.Last(m => m.Recipient == email);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        await browser.GetAsync($"/account/confirm-email?userId={user.Id}&token=tampered");
        var invalid = await browser.Submit("/account/confirm-email", []);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("invalid or expired", await invalid.Content.ReadAsStringAsync());

        // Deterministically expire Identity tokens without sleeping or changing the machine clock.
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;
        var original = options.TokenLifespan;
        try
        {
            options.TokenLifespan = TimeSpan.FromSeconds(-1);
            await browser.GetAsync(message.ConfirmationLink.PathAndQuery);
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.Submit("/account/confirm-email", [])).StatusCode);
        }
        finally { options.TokenLifespan = original; }
        await scope.ServiceProvider.GetRequiredService<FinSight.Infrastructure.Persistence.FinSightDbContext>().Entry(user).ReloadAsync();
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task SendFailure_PreservesAccount_ResendIsGenericAndRateLimited()
    {
        using var browser = factory.Browser();
        var email = $"resend-{Guid.NewGuid():N}@example.com";
        factory.EmailSender.FailDelivery = true;
        try
        {
            Assert.Equal(HttpStatusCode.Redirect, (await browser.Submit("/account/register", WebsiteTestClient.Registration(email))).StatusCode);
        }
        finally { factory.EmailSender.FailDelivery = false; }
        using var scope = factory.Services.CreateScope();
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email));
        Assert.DoesNotContain(factory.EmailSender.Messages, m => m.Recipient == email);

        var existing = await browser.Submit("/account/resend-confirmation", new() { ["Email"] = email });
        var missing = await browser.Submit("/account/resend-confirmation", new() { ["Email"] = "missing@example.com" });
        Assert.Equal(existing.StatusCode, missing.StatusCode);
        Assert.Equal(existing.Headers.Location, missing.Headers.Location);
        await browser.Confirm(factory.EmailSender.Messages.Last(m => m.Recipient == email));
        var before = factory.EmailSender.Messages.Count;
        var confirmed = await browser.Submit("/account/resend-confirmation", new() { ["Email"] = email });
        Assert.Equal(existing.Headers.Location, confirmed.Headers.Location);
        Assert.Equal(before, factory.EmailSender.Messages.Count);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await browser.Submit("/account/resend-confirmation", new() { ["Email"] = email })).StatusCode);
    }
}
