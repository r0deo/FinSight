using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.IntegrationTests.Infrastructure;

// Only Google's network responses are substituted. State, correlation, PKCE, external
// cookies, Identity, PostgreSQL transactions and application authorization remain real.
public sealed class GoogleApiFactory : FinSightApiFactory
{
    public GoogleBackchannel Backchannel { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Authentication:Google:Enabled", "true");
        builder.UseSetting("Authentication:Google:ClientId", "test-client");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-secret-not-real");
        builder.ConfigureServices(services => services.PostConfigure<GoogleOptions>(GoogleDefaults.AuthenticationScheme,
            options => options.Backchannel = new HttpClient(Backchannel)));
    }
}

public sealed class GoogleBackchannel : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, object> _identities = new();

    public string Add(string subject, string email, bool? verified = true)
    {
        var code = Guid.NewGuid().ToString("N");
        var data = new Dictionary<string, object> { ["sub"] = subject, ["email"] = email, ["name"] = "Test Owner" };
        if (verified.HasValue) data["email_verified"] = verified.Value;
        _identities[code] = data;
        return code;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("/token"))
        {
            var fields = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal("https://localhost:7080/signin-google", fields["redirect_uri"].ToString());
            Assert.False(string.IsNullOrWhiteSpace(fields["code_verifier"]));
            var code = fields["code"].ToString();
            Assert.True(_identities.ContainsKey(code));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = code, token_type = "Bearer", expires_in = 3600 }) };
        }
        Assert.Equal("https://openidconnect.googleapis.com/v1/userinfo", request.RequestUri.AbsoluteUri);
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(_identities[request.Headers.Authorization!.Parameter!]) };
    }
}
