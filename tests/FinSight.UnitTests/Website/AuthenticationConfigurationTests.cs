using FinSight.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace FinSight.UnitTests.Website;

public class AuthenticationConfigurationTests
{
    [Theory]
    [InlineData("http://app.example.com", true, "smtp-user", "smtp-key")]
    [InlineData("https://localhost:7080", true, "smtp-user", "smtp-key")]
    [InlineData("https://app.example.com/path", true, "smtp-user", "smtp-key")]
    [InlineData("https://app.example.com", false, "smtp-user", "smtp-key")]
    [InlineData("https://app.example.com", true, "", "smtp-key")]
    [InlineData("https://app.example.com", true, "smtp-user", "")]
    public void ProductionEmail_RejectsUnsafeConfiguration(string origin, bool tls, string username, string password)
    {
        var result = new EmailOptionsValidator(new TestEnvironment("Production")).Validate(null, new EmailOptions
        {
            WebsiteBaseUrl = origin, Host = "smtp-relay.brevo.com", Port = 587,
            FromAddress = "owner@example.com", EnableSsl = tls, Username = username, Password = password
        });
        Assert.True(result.Failed);
    }

    [Fact]
    public void ProductionEmail_AcceptsAuthenticatedTlsAndTrustedOrigin()
    {
        Assert.True(new EmailOptionsValidator(new TestEnvironment("Production")).Validate(null, new EmailOptions
        {
            WebsiteBaseUrl = "https://app.example.com", Host = "smtp-relay.brevo.com", Port = 587,
            FromAddress = "owner@example.com", EnableSsl = true, Username = "smtp-user", Password = "smtp-key"
        }).Succeeded);
    }

    [Fact]
    public void ProductionDataProtection_RequiresCertificateEvenWhenOldDiskPathIsConfigured()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddFinSightDataProtection(
            new ConfigurationBuilder().Build(), new TestEnvironment("Production")));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["DataProtection:KeysPath"] = Path.GetTempPath() }).Build();
        Assert.Throws<InvalidOperationException>(() => services.AddFinSightDataProtection(config, new TestEnvironment("Production")));
    }

    [Fact]
    public void PersistedProtectionKeys_CanUnprotectAfterServiceProviderRestart()
    {
        var directory = Directory.CreateTempSubdirectory("finsight-key-test-");
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["DataProtection:KeysPath"] = directory.FullName }).Build();
            ServiceProvider CreateServices()
            {
                var services = new ServiceCollection().AddLogging();
                services.AddFinSightDataProtection(config, new TestEnvironment("Development"));
                return services.BuildServiceProvider();
            }
            string protectedValue;
            using (var first = CreateServices())
                protectedValue = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-test").Protect("confirmation-state");
            using var second = CreateServices();
            Assert.Equal("confirmation-state", second.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("restart-test").Unprotect(protectedValue));
            Assert.NotEmpty(directory.GetFiles("key-*.xml"));
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "FinSight";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
