using FinSight.Api.Hosting;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FinSight.UnitTests.Website;

public class DeploymentConfigurationTests
{
    [Theory]
    [InlineData("Disable")]
    [InlineData("Prefer")]
    [InlineData("Require")]
    [InlineData("VerifyCA")]
    public void NonDevelopmentPostgres_RejectsTlsWithoutHostnameVerification(string mode)
    {
        var config = Configuration($"Host=example.neon.tech;Database=pilot;Username=owner;Password=test;SSL Mode={mode}");
        Assert.Throws<InvalidOperationException>(() => PostgresConfiguration.ConnectionString(config, true));
    }

    [Fact]
    public void NeonConnection_UsesCertificateAndHostnameValidation()
    {
        var config = Configuration("Host=example.neon.tech;Database=pilot;Username=owner;Password=test;SSL Mode=VerifyFull;Channel Binding=Require");
        var parsed = new NpgsqlConnectionStringBuilder(PostgresConfiguration.ConnectionString(config, true));
        Assert.Equal(SslMode.VerifyFull, parsed.SslMode);
        Assert.Equal(ChannelBinding.Require, parsed.ChannelBinding);
        Assert.Equal("example.neon.tech", parsed.Host);
    }

    [Theory]
    [InlineData("postgresql://owner:secret@example.neon.tech/pilot")]
    [InlineData("Password=secret;UnknownSetting=bad")]
    public void InvalidConnection_ErrorsDoNotEchoCredentials(string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() => PostgresConfiguration.ConnectionString(Configuration(value), true));
        Assert.DoesNotContain("secret", error.ToString());
    }

    [Theory]
    [InlineData("http://pilot.onrender.com", "pilot.onrender.com")]
    [InlineData("https://pilot.onrender.com", "*")]
    [InlineData("https://pilot.onrender.com", "other.onrender.com")]
    [InlineData("https://pilot.onrender.com/evil", "pilot.onrender.com")]
    [InlineData("https://pilot.onrender.com.evil.test", "pilot.onrender.com.evil.test")]
    public void RenderProfile_RejectsUntrustedOriginsAndWildcardHosts(string origin, string hosts) =>
        Assert.Throws<InvalidOperationException>(() => RenderHosting.ValidateOrigin(origin, hosts));

    [Fact]
    public async Task RenderProfile_IgnoresForgedForwardedHeaders_AndUsesConfiguredHttpsOrigin()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("pilot.onrender.com");
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "http";
        context.Request.Headers["X-Forwarded-Host"] = "evil.example";
        context.Request.Headers["X-Forwarded-For"] = "1.2.3.4";
        var called = false;
        await RenderHosting.NormalizeHttps(context, _ => { called = true; return Task.CompletedTask; },
            RenderHosting.ValidateOrigin("https://pilot.onrender.com", "pilot.onrender.com"));
        Assert.True(called);
        Assert.True(context.Request.IsHttps);
        Assert.Equal("pilot.onrender.com", context.Request.Host.Value);
        Assert.Null(context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task RenderProfile_RejectsWrongHostEvenWhenForwardedHostMatches()
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("evil.example");
        context.Request.Headers["X-Forwarded-Host"] = "pilot.onrender.com";
        var called = false;
        await RenderHosting.NormalizeHttps(context, _ => { called = true; return Task.CompletedTask; },
            new Uri("https://pilot.onrender.com"));
        Assert.False(called);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task DisabledEmailSender_DoesNotAttemptDelivery() =>
        await Assert.ThrowsAsync<System.Net.Mail.SmtpException>(() => new SmtpEmailSender(
            Microsoft.Extensions.Options.Options.Create(new EmailOptions { Enabled = false }))
            .SendAsync("owner@gmail.com", "test", "body"));

    private static IConfiguration Configuration(string value) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = value }).Build();
}
