using System.Collections.Concurrent;
using System.Net;
using FinSight.Infrastructure.Persistence;
using FinSight.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FinSight.IntegrationTests;

public partial class WebsiteTests
{
    [Fact]
    public async Task BusinessCosts_MissingMigrationFailsExplicitly_ThenMigratedAuthenticatedNavigationWorks()
    {
        // An isolated disposable database, never the developer or production database.
        var isolated = new FinSightApiFactory();
        await isolated.InitializeAsync();
        try
        {
            using var logs = new ExceptionLog();
            isolated.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
            using (var scope = isolated.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<FinSightDbContext>()
                    .GetService<IMigrator>().MigrateAsync("20261013120101_AddWhatIfSimulations");

            var journey = new WebsiteTests(isolated);
            using var browser = journey.Browser();
            await Login(browser, await journey.RegisterAndConfirm(browser));
            Assert.Contains("href=\"/business-costs\"", await browser.GetStringAsync("/business-costs/create"));
            var broken = await browser.GetAsync("/business-costs?month=2026-10");
            Assert.Equal(HttpStatusCode.InternalServerError, broken.StatusCode);
            Assert.Contains(logs.Exceptions, e => e is PostgresException
                { SqlState: PostgresErrorCodes.UndefinedTable } p && p.MessageText.Contains("BusinessCosts"));
            Assert.DoesNotContain("No costs entered", await broken.Content.ReadAsStringAsync());

            using (var scope = isolated.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Database.MigrateAsync();
            var repaired = await browser.GetAsync("/business-costs?month=2026-10");
            Assert.Equal(HttpStatusCode.OK, repaired.StatusCode);
            Assert.Contains("No costs entered", await repaired.Content.ReadAsStringAsync());
        }
        finally { await isolated.DisposeAsync(); }
    }

    private sealed class ExceptionLog(string? category = null) : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<Exception> Exceptions { get; } = new();
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => category is null || categoryName == category
            ? this : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null)
            {
                Exceptions.Enqueue(exception);
                Messages.Enqueue(formatter(state, exception));
            }
        }
        public void Dispose() { }
    }
}
