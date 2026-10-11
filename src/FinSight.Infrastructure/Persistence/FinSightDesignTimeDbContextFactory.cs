using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FinSight.Infrastructure.Persistence;

// Explicit migrations do not require a running website, Google, SMTP or a DP certificate.
public sealed class FinSightDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FinSightDbContext>
{
    public FinSightDbContext CreateDbContext(string[] args)
    {
        var development = (Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")) == "Development";
        var builder = new ConfigurationBuilder();
        if (development)
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "FinSight.Api", "appsettings.Development.json")
            };
            var developmentSettings = candidates.FirstOrDefault(File.Exists);
            if (developmentSettings is not null) builder.AddJsonFile(developmentSettings);
        }
        var configuration = builder.AddEnvironmentVariables().Build();
        return new FinSightDbContext(new DbContextOptionsBuilder<FinSightDbContext>()
            .UseNpgsql(PostgresConfiguration.ConnectionString(configuration, !development)).Options);
    }
}
