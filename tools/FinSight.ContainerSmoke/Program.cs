using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using FinSight.Infrastructure;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

// Local-only verification. Never connects to Neon or deploys to Render. Testcontainers
// owns and removes its uniquely named containers/network; no user database is touched.
var image = args.FirstOrDefault() ?? "finsight:pilot";
var directory = Directory.CreateTempSubdirectory("finsight-smoke-");
await using var network = new NetworkBuilder().Build();
await network.CreateAsync();
using var rsa = RSA.Create(2048);
var request = new CertificateRequest("CN=finsight-smoke-db", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
var names = new SubjectAlternativeNameBuilder();
names.AddDnsName("finsight-smoke-db");
names.AddDnsName("localhost");
names.AddIpAddress(IPAddress.Loopback);
request.CertificateExtensions.Add(names.Build());
using var serverCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
var rootPath = Path.Combine(directory.FullName, "postgres-root.crt");
await File.WriteAllTextAsync(rootPath, serverCertificate.ExportCertificatePem());
using var keyRsa = RSA.Create(2048);
using var keyCertificate = new CertificateRequest("CN=FinSight DP smoke", keyRsa, HashAlgorithmName.SHA256,
    RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
var certificatePassword = Guid.NewGuid().ToString("N");
var keyBytes = Encoding.ASCII.GetBytes(Convert.ToBase64String(keyCertificate.Export(X509ContentType.Pfx, certificatePassword)));
var keyPath = Path.Combine(directory.FullName, "dp.b64");
await File.WriteAllBytesAsync(keyPath, keyBytes);
var signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

try
{
    await using var postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithNetwork(network).WithNetworkAliases("finsight-smoke-db")
        .WithResourceMapping(Encoding.ASCII.GetBytes(serverCertificate.ExportCertificatePem()), "/tmp/server.crt", uid: 70, gid: 70)
        .WithResourceMapping(Encoding.ASCII.GetBytes(rsa.ExportPkcs8PrivateKeyPem()), "/tmp/server.key", uid: 70, gid: 70,
            fileMode: UnixFileModes.UserRead | UnixFileModes.UserWrite)
        .WithCommand("-c", "ssl=on", "-c", "ssl_cert_file=/tmp/server.crt", "-c", "ssl_key_file=/tmp/server.key")
        .Build();
    await postgres.StartAsync();
    var local = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
    { SslMode = SslMode.VerifyFull, RootCertificate = rootPath };
    var internalConnection = new NpgsqlConnectionStringBuilder(local.ConnectionString)
    { Host = "finsight-smoke-db", Port = 5432, RootCertificate = "/etc/secrets/postgres-root.crt" };

    await using var database = new FinSightDbContext(new DbContextOptionsBuilder<FinSightDbContext>().UseNpgsql(local.ConnectionString).Options);
    // Explicit test provisioning, never part of application startup.
    await database.Database.MigrateAsync();
    await database.Database.OpenConnectionAsync();
    await using (var command = database.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid()";
        Check(Equals(true, await command.ExecuteScalarAsync()), "PostgreSQL connection must use TLS.");
    }
    await database.Database.CloseConnectionAsync();

    IContainer CreateApp() => new ContainerBuilder(image).WithNetwork(network).WithPortBinding(10000, true)
        .WithEnvironment("Hosting__Platform", "Render")
        .WithEnvironment("AllowedHosts", "pilot.onrender.com")
        .WithEnvironment("Email__WebsiteBaseUrl", "https://pilot.onrender.com")
        .WithEnvironment("Email__Enabled", "false")
        .WithEnvironment("Jwt__SigningKey", signingKey)
        .WithEnvironment("ConnectionStrings__DefaultConnection", internalConnection.ConnectionString)
        .WithEnvironment("DataProtection__CertificateBase64File", "/etc/secrets/dp.b64")
        .WithEnvironment("DataProtection__CertificatePassword", certificatePassword)
        .WithResourceMapping(keyBytes, "/etc/secrets/dp.b64", uid: 1654, gid: 1654, fileMode: UnixFileModes.UserRead)
        .WithResourceMapping(Encoding.ASCII.GetBytes(serverCertificate.ExportCertificatePem()), "/etc/secrets/postgres-root.crt")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(10000)).Build();

    var cookies = new Dictionary<string, string>();
    HttpClient Client(IContainer app)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { BaseAddress = new Uri($"http://localhost:{app.GetMappedPublicPort(10000)}") };
        client.DefaultRequestHeaders.Host = "pilot.onrender.com";
        return client;
    }
    async Task<HttpResponseMessage> Send(HttpClient client, string path, HttpContent? content = null)
    {
        using var message = new HttpRequestMessage(content is null ? HttpMethod.Get : HttpMethod.Post, path) { Content = content };
        if (cookies.Count > 0) message.Headers.Add("Cookie", string.Join("; ", cookies.Select(pair => $"{pair.Key}={pair.Value}")));
        var response = await client.SendAsync(message);
        if (response.Headers.TryGetValues("Set-Cookie", out var headers))
            foreach (var header in headers)
            {
                var pair = header.Split(';')[0].Split('=', 2);
                cookies[pair[0]] = pair[1];
            }
        return response;
    }

    await using var first = CreateApp();
    await first.StartAsync();
    using var browser = Client(first);
    Check((await Send(browser, "/health")).StatusCode == HttpStatusCode.OK, "Health endpoint.");
    Check((await Send(browser, "/css/site.css")).StatusCode == HttpStatusCode.OK, "Published static assets.");
    var page = await Send(browser, "/account/login");
    var html = await page.Content.ReadAsStringAsync();
    Check(page.IsSuccessStatusCode && html.Contains("Log in to FinSight"), "Published MVC view.");
    var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
    var antiforgery = WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value);
    Check(!string.IsNullOrEmpty(antiforgery), "Antiforgery form.");
    Check(!html.Contains("Continue with Google"), "Google remains disabled without credentials.");
    Check((await Send(browser, "/api/products")).StatusCode == HttpStatusCode.Unauthorized, "API unauthenticated is 401.");

    var email = "container-smoke@example.com";
    var registration = await Send(browser, "/api/auth/register", JsonContent.Create(new
    { Email = email, Password = "Password1!", BusinessName = "Smoke Cafe", Location = "Baghdad", Currency = "IQD", ContactInformation = email }));
    Check(registration.IsSuccessStatusCode, "Registration while email is disabled retains unconfirmed account.");
    var responseJson = await registration.Content.ReadFromJsonAsync<JsonElement>();
    var userId = responseJson.GetProperty("userId").GetGuid();
    Check((await Send(browser, "/api/auth/login", JsonContent.Create(new { Email = email, Password = "Password1!" }))).StatusCode
        == HttpStatusCode.Unauthorized, "Unconfirmed API login rejected.");

    // Generate a real Identity confirmation token against the same DB/key ring for this
    // test account, without needing a live SMTP provider or exposing a test endpoint.
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = local.ConnectionString,
        ["DataProtection:CertificateBase64File"] = keyPath, ["DataProtection:CertificatePassword"] = certificatePassword,
        ["Email:Enabled"] = "false", ["Email:WebsiteBaseUrl"] = "https://pilot.onrender.com"
    }).Build();
    var services = new ServiceCollection().AddLogging();
    var environment = new ProductionEnvironment();
    services.AddSingleton<IHostEnvironment>(environment);
    services.AddHttpContextAccessor();
    services.AddInfrastructure(configuration, requireVerifiedTls: true);
    services.AddFinSightDataProtection(configuration, environment);
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var user = (await users.FindByIdAsync(userId.ToString()))!;
    var token = await users.GenerateEmailConfirmationTokenAsync(user);
    Check((await Send(browser, "/api/auth/confirm-email", JsonContent.Create(new { UserId = userId, Token = token }))).IsSuccessStatusCode,
        "Real confirmation token accepted by container.");
    var login = await Send(browser, "/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
    { ["Email"] = email, ["Password"] = "Password1!", ["__RequestVerificationToken"] = antiforgery }));
    Check(login.StatusCode == HttpStatusCode.Redirect && cookies.ContainsKey("__Host-FinSight.Auth"), "Website login cookie.");
    Check((await Send(browser, "/products")).IsSuccessStatusCode, "Authenticated products view.");
    Check((await Send(browser, "/api/products")).StatusCode == HttpStatusCode.Unauthorized, "Cookie cannot authenticate API.");
    var jwtResponse = await Send(browser, "/api/auth/login", JsonContent.Create(new { Email = email, Password = "Password1!" }));
    Check(jwtResponse.IsSuccessStatusCode, "Confirmed password API token issuance.");
    var jwt = (await jwtResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
    browser.DefaultRequestHeaders.Authorization = new("Bearer", jwt);
    Check((await Send(browser, "/api/products")).IsSuccessStatusCode, "JWT API access.");
    browser.DefaultRequestHeaders.Authorization = null;
    var invalidHost = new HttpRequestMessage(HttpMethod.Get, "/health");
    invalidHost.Headers.Host = "evil.example";
    invalidHost.Headers.Add("X-Forwarded-Host", "pilot.onrender.com");
    Check((await browser.SendAsync(invalidHost)).StatusCode == HttpStatusCode.BadRequest, "Forged host rejected.");
    var inspection = await first.ExecAsync(["sh", "-c", "test ! -f /app/appsettings.Development.json && test ! -d /app/.git && test $(id -u) != 0"]);
    Check(inspection.ExitCode == 0, "Non-root image excludes development settings and git metadata.");

    await first.StopAsync();
    await using var recreated = CreateApp();
    await recreated.StartAsync();
    using var restartedBrowser = Client(recreated);
    Check((await Send(restartedBrowser, "/products")).IsSuccessStatusCode, "Original auth cookie survives container recreation.");
    var keys = await database.DataProtectionKeys.AsNoTracking().ToListAsync();
    Check(keys.Count > 0 && keys.All(key => key.Xml!.Contains("encryptedSecret")), "Database key ring encrypted at rest.");
    Console.WriteLine("PASS: production container, verified PostgreSQL TLS, health, MVC/static files, disabled email, confirmation, cookie/JWT separation, host rejection, non-root image, encrypted key ring and cookie survival after container recreation.");
}
finally { directory.Delete(recursive: true); }

static void Check(bool passed, string name)
{
    if (!passed) throw new InvalidOperationException("Smoke check failed: " + name);
}

sealed class ProductionEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "FinSight";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
