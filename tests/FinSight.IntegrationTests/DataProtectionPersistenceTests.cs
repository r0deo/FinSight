using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using FinSight.Infrastructure;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using FinSight.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FinSight.IntegrationTests;

public class DataProtectionPersistenceTests(FinSightApiFactory factory) : IClassFixture<FinSightApiFactory>
{
    [Fact]
    public async Task PostgreSqlKeyRing_PreservesCookieAndIdentityTokenAcrossInstances_AndRequiresCertificate()
    {
        using var browser = factory.Browser();
        var email = $"restart-{Guid.NewGuid():N}@example.com";
        await browser.Submit("/account/register", WebsiteTestClient.Registration(email));
        var directory = Directory.CreateTempSubdirectory("finsight-certificate-test-");
        var password = Guid.NewGuid().ToString("N");
        using var rsa = RSA.Create(2048);
        using var certificate = new CertificateRequest("CN=FinSight test", rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certificateFile = Path.Combine(directory.FullName, "certificate.b64");
        await File.WriteAllTextAsync(certificateFile, Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, password)));
        using var dbScope = factory.Services.CreateScope();
        var database = dbScope.ServiceProvider.GetRequiredService<FinSightDbContext>();

        ServiceProvider CreateInstance(string path)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = database.Database.GetConnectionString(),
                ["DataProtection:CertificateBase64File"] = path,
                ["DataProtection:CertificatePassword"] = password,
                ["Email:Enabled"] = "false", ["Email:WebsiteBaseUrl"] = "https://pilot.onrender.com",
                ["Jwt:Issuer"] = "tests", ["Jwt:Audience"] = "tests", ["Jwt:SigningKey"] = new string('t', 64)
            }).Build();
            var services = new ServiceCollection().AddLogging();
            var environment = new ProductionEnvironment();
            services.AddSingleton<IHostEnvironment>(environment);
            services.AddHttpContextAccessor();
            // Isolated Testcontainers PostgreSQL is plaintext. Production connection-policy
            // validation is covered separately; key persistence/encryption here is real.
            services.AddInfrastructure(config);
            services.AddFinSightDataProtection(config, environment);
            return services.BuildServiceProvider();
        }

        try
        {
            string cookie;
            string token;
            Guid userId;
            using (var first = CreateInstance(certificateFile))
            using (var scope = first.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = (await users.FindByEmailAsync(email))!;
                userId = user.Id;
                token = await users.GenerateEmailConfirmationTokenAsync(user);
                var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], IdentityConstants.ApplicationScheme)),
                    new AuthenticationProperties(), IdentityConstants.ApplicationScheme);
                cookie = first.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                    .Get(IdentityConstants.ApplicationScheme).TicketDataFormat.Protect(ticket);
            }

            var rows = await database.DataProtectionKeys.AsNoTracking().ToListAsync();
            Assert.NotEmpty(rows);
            Assert.All(rows, row =>
            {
                var xml = XDocument.Parse(row.Xml!);
                Assert.Contains(xml.Descendants(), element => element.Name.LocalName == "encryptedSecret");
                Assert.DoesNotContain(xml.Descendants(), element => element.Name.LocalName == "masterKey");
            });
            using (var second = CreateInstance(certificateFile))
            using (var scope = second.CreateScope())
            {
                var restored = second.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                    .Get(IdentityConstants.ApplicationScheme).TicketDataFormat.Unprotect(cookie);
                Assert.Equal(userId.ToString(), restored!.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = (await users.FindByEmailAsync(email))!;
                Assert.True((await users.ConfirmEmailAsync(user, token)).Succeeded);
            }

            using var wrongRsa = RSA.Create(2048);
            using var wrongCertificate = new CertificateRequest("CN=Wrong certificate", wrongRsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            var wrongPath = Path.Combine(directory.FullName, "wrong.b64");
            await File.WriteAllTextAsync(wrongPath, Convert.ToBase64String(wrongCertificate.Export(X509ContentType.Pfx, password)));
            using var wrong = CreateInstance(wrongPath);
            Assert.Null(wrong.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme).TicketDataFormat.Unprotect(cookie));
        }
        finally { directory.Delete(recursive: true); }
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "FinSight";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
