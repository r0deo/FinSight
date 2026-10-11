using System.Security;
using System.Text.Json;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

const string requiredMigration = "20261011105742_PersistDataProtectionKeys";
var resultPath = Path.Combine(AppContext.BaseDirectory, "migration-result.json");
var result = new MigrationReport("AwaitingLocalInput", [], [], false, false);
await SaveReport();
try
{
    if (args.Length != 0 || Console.IsInputRedirected)
        throw new SafeFailure("Run interactively without arguments. Never put the URL in command arguments.");
    Console.WriteLine("FinSight: explicit Neon migrations (website will NOT start)");
    Console.WriteLine("Paste DIRECT DATABASE_URL at the masked prompt. Do not paste it in chat.");
    Console.Write("Direct URL: ");
    using var secret = new SecureString();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Escape) throw new SafeFailure("Cancelled before connecting.");
        if (key.Key == ConsoleKey.Backspace)
        {
            if (secret.Length > 0) { secret.RemoveAt(secret.Length - 1); Console.Write("\b \b"); }
        }
        else if (!char.IsControl(key.KeyChar) && secret.Length < 16384)
        { secret.AppendChar(key.KeyChar); Console.Write('*'); }
    }
    Console.WriteLine();
    string connectionString;
    try { connectionString = NeonMigrationConnection.FromDirectUrl(new System.Net.NetworkCredential("", secret).Password); }
    catch (ArgumentException) { throw new SafeFailure("URL rejected. Use the unquoted direct Neon URL (not pooled), with standard TLS options. No changes applied."); }
    secret.Clear();
    var settings = new NpgsqlConnectionStringBuilder(connectionString);
    // Non-secret target identifiers displayed only in this local console, never in the report.
    Console.WriteLine("Target host: " + settings.Host);
    Console.WriteLine("Target database: " + settings.Database);
    Console.WriteLine("TLS: VerifyFull with channel binding. No customer data will be seeded.");
    Console.Write("Confirm this is the intended Neon branch/database by typing APPLY: ");
    if (Console.ReadLine() != "APPLY") throw new SafeFailure("Cancelled before connecting.");

    await using var database = new FinSightDbContext(new DbContextOptionsBuilder<FinSightDbContext>()
        .UseNpgsql(connectionString).Options);
    connectionString = string.Empty;
    settings.Clear();
    if (database.Database.HasPendingModelChanges())
        throw new SafeFailure("The compiled model has changes without a migration. No migration was created or applied.");
    var known = database.Database.GetMigrations().ToArray();
    if (!known.Contains(requiredMigration)) throw new SafeFailure("Required key-storage migration missing from this build.");
    // FromDirectUrl enforces VerifyFull + required channel binding and disables GSS.
    // With no validation overrides, OpenConnectionAsync fails if certificate/hostname
    // validation fails. pg_stat_ssl describes the proxy-to-backend connection, NOT
    // this client's TLS connection to Neon's proxy, so it must not be used here.
    await database.Database.OpenConnectionAsync();
    result = result with { VerifiedTls = true };
    Console.WriteLine("Connected using certificate-validated TLS and required channel binding.");
    var before = (await database.Database.GetAppliedMigrationsAsync()).ToArray();
    if (before.Except(known).Any()) throw new SafeFailure("Database has migrations unknown to this checkout. Stopped for review.");
    var pending = (await database.Database.GetPendingMigrationsAsync()).ToArray();
    Console.WriteLine("Existing applied migrations: " + before.Length);
    foreach (var migration in pending) Console.WriteLine("Pending: " + migration);
    result = new("Applying", before, pending, true, false);
    await SaveReport();
    await database.Database.MigrateAsync();
    // Read the actual persisted EF migration history again, not an inferred success list.
    var after = (await database.Database.GetAppliedMigrationsAsync()).ToArray();
    var remaining = (await database.Database.GetPendingMigrationsAsync()).ToArray();
    await using var schema = database.Database.GetDbConnection().CreateCommand();
    schema.CommandText = "SELECT to_regclass('public.\"DataProtectionKeys\"') IS NOT NULL";
    var tableExists = Equals(await schema.ExecuteScalarAsync(), true);
    if (remaining.Length != 0 || !after.Contains(requiredMigration) || !tableExists)
        throw new SafeFailure("Post-migration verification failed. Re-run to inspect current history; migrations may have been applied.");
    result = new("Succeeded", after, [], true, true);
    Console.WriteLine("SUCCESS: all existing migrations applied and history verified.");
    foreach (var migration in after) Console.WriteLine("Applied: " + migration);
}
catch (SafeFailure failure)
{
    result = result with { Status = "Stopped: " + failure.Message };
    Console.WriteLine(result.Status);
    Environment.ExitCode = 1;
}
catch (Exception exception)
{
    // Never log provider messages/stack traces: they can contain user input or credentials.
    var reason = exception switch
    {
        PostgresException postgres => "PostgreSQL SQLSTATE " + postgres.SqlState,
        NpgsqlException => "PostgreSQL connectivity/TLS error",
        _ => "Migration execution error"
    };
    result = result with { Status = "Failed: " + reason + ". Applied state requires rechecking; no credentials logged." };
    Console.WriteLine(result.Status);
    Environment.ExitCode = 1;
}
finally
{
    await SaveReport();
    Console.WriteLine("A credential-free result was saved beside this executable.");
    if (!Console.IsInputRedirected)
    {
        Console.WriteLine("Press any key to close.");
        Console.ReadKey(intercept: true);
    }
}

async Task SaveReport() => await File.WriteAllTextAsync(resultPath,
    JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));

sealed record MigrationReport(string Status, string[] AppliedMigrations, string[] PendingMigrations,
    bool VerifiedTls, bool DataProtectionKeysTableExists);
sealed class SafeFailure(string message) : Exception(message);
