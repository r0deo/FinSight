using FinSight.Application.Common.Interfaces;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinSight.IntegrationTests.Infrastructure;

public class FinSightApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly TestPostgresDatabase _database = new();

    public FakeEmailSender EmailSender { get; } = new();

    public MutableTimeProvider TimeProvider { get; } = new();

    // Compatibility tests can start a fresh disposable database at a deployed schema.
    // Never downgrade a populated database to simulate a missing migration.
    protected virtual string? InitialMigration => null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // The growing shared-fixture suite performs many real logins from one test IP.
        // Keep a test-only allowance; production defaults and resend-limit tests are unchanged.
        builder.UseSetting("RateLimiting:AuthenticationPermitLimit", "1000");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _database.GetConnectionString(),
                ["Jwt:SigningKey"] = new string('x', 64),
                ["Email:Host"] = "localhost",
                ["Email:Port"] = "1025",
                ["Email:FromAddress"] = "tests@finsight.local",
                ["RateLimiting:AuthenticationPermitLimit"] = "1000"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FinSightDbContext>>();
            services.RemoveAll<FinSightDbContext>();
            services.AddDbContext<FinSightDbContext>(options =>
                options.UseNpgsql(_database.GetConnectionString()));
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(EmailSender);
            services.AddSingleton<IEmailSender>(EmailSender);
            services.AddSingleton<TimeProvider>(TimeProvider);
        });
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
        await dbContext.GetService<IMigrator>().MigrateAsync(InitialMigration);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
