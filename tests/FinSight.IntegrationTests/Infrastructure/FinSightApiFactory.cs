using FinSight.Application.Common.Interfaces;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace FinSight.IntegrationTests.Infrastructure;

public sealed class FinSightApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public FakeEmailSender EmailSender { get; } = new();

    public MutableTimeProvider TimeProvider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("RateLimiting:AuthenticationPermitLimit", "100");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _database.GetConnectionString(),
                ["Jwt:SigningKey"] = new string('x', 64),
                ["Email:Host"] = "localhost",
                ["Email:Port"] = "1025",
                ["Email:FromAddress"] = "tests@finsight.local",
                ["RateLimiting:AuthenticationPermitLimit"] = "100"
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
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
