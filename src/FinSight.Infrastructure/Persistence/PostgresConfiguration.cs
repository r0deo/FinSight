using Microsoft.Extensions.Configuration;
using Npgsql;

namespace FinSight.Infrastructure.Persistence;

public static class PostgresConfiguration
{
    public static string ConnectionString(IConfiguration configuration, bool requireVerifiedTls)
    {
        var value = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required in Npgsql key=value format.");
        NpgsqlConnectionStringBuilder settings;
        try { settings = new(value); }
        catch (ArgumentException)
        {
            // Do not echo the supplied connection string or parser exception (may contain credentials).
            throw new InvalidOperationException("Invalid PostgreSQL connection configuration. Use Npgsql key=value format.");
        }
        if (requireVerifiedTls && settings.SslMode != SslMode.VerifyFull)
            throw new InvalidOperationException("Non-development PostgreSQL requires SSL Mode=VerifyFull for certificate and hostname validation.");
        return settings.ConnectionString;
    }
}
