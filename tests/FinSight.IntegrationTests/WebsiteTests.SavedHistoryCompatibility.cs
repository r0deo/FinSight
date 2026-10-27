using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using FinSight.Domain.Services;
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
    private sealed class PreCostLinkFactory : FinSightApiFactory
    {
        protected override string? InitialMigration => "20261019084302_AddDeliverySimulationSnapshot";
    }

    [Fact]
    public async Task History_PreCostLinkSchema_LogsExactFailure_ExistingMigrationRestoresRowsWithoutRewriting()
    {
        await using var isolated = new PreCostLinkFactory();
        await isolated.InitializeAsync();
        using var logs = new ExceptionLog(typeof(FinSight.Api.Website.WebsiteExceptionFilter).FullName);
        isolated.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        var journey = new WebsiteTests(isolated);
        using var owner = journey.Browser();
        var email = await journey.RegisterAndConfirm(owner);
        await Login(owner, email);
        var id = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var created = DateTime.UtcNow;
        string historicalRow;
        using (var scope = isolated.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == email);
            db.Products.Add(new Product(productId, user.BusinessId, "Historical coffee", "Drinks", null, 9000, 6000));
            await db.SaveChangesAsync();
            // This represents an existing row on the previous schema. Today's EF mapping
            // cannot INSERT against that schema because the new columns do not exist yet.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PricingSimulations" ("Id","BusinessId","ProductId","CreatedByUserId",
                    "CurrentPrice","UnitCost","ProposedPrice","CurrentSalesQuantity","ExpectedSalesQuantity",
                    "CurrentContribution","ProposedContribution","Difference","IsProposedPriceBelowCost",
                    "Recommendation","CreatedAtUtc","UpdatedAtUtc","Version","CalculationVersion",
                    "CurrencySnapshot","ProductNameSnapshot")
                VALUES ({id},{user.BusinessId},{productId},{user.Id},4000,1000,4500,100,90,
                    300000,315000,15000,FALSE,'Recommended',{created},{created},1,1,'IQD','Historical coffee')
                """);
            historicalRow = await db.Database.SqlQuery<string>(
                $"""SELECT to_jsonb(p)::text AS "Value" FROM "PricingSimulations" p WHERE "Id"={id}""").SingleAsync();
        }

        // Navigate through the authenticated page's real saved-history link.
        var navigation = await owner.GetStringAsync("/simulations/preview");
        Assert.Contains("href=\"/simulations\"", navigation);
        foreach (var path in new[] { "/simulations", $"/simulations/{id}" })
        {
            logs.Exceptions.Clear();
            logs.Messages.Clear();
            var broken = await owner.GetAsync(path + "?diagnosticProbe=must-not-enter-error-log");
            Assert.Equal(HttpStatusCode.InternalServerError, broken.StatusCode);
            var error = Assert.IsType<PostgresException>(logs.Exceptions.First(e => e is PostgresException));
            Assert.Equal(PostgresErrorCodes.UndefinedColumn, error.SqlState);
            Assert.Contains("AdditionalDecisionCost", error.MessageText);
            Assert.Contains(logs.Messages, message => message.Contains($"GET {path} TraceId"));
            Assert.DoesNotContain(logs.Messages, message => message.Contains("must-not-enter-error-log"));
            // Actual server-side exception captured from ILogger, with no credentials or SQL values.
            Console.WriteLine($"Authenticated {path}: {error.GetType().Name} SQLSTATE {error.SqlState}: {error.MessageText}\n{error.StackTrace}");
            Assert.Contains("SavedSimulationPagesController", error.StackTrace);
            if (path == "/simulations")
                Assert.Contains("PricingSimulationRepository.GetPagedAsync", error.StackTrace);
            var publicHtml = await broken.Content.ReadAsStringAsync();
            Assert.DoesNotContain("AdditionalDecisionCost", publicHtml);
            Assert.DoesNotContain("Npgsql", publicHtml);
            Assert.DoesNotContain("No saved simulations yet", publicHtml);
        }

        using (var scope = isolated.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            Assert.Equal(["20261019153847_AddOptionalSimulationBusinessCosts"], await db.Database.GetPendingMigrationsAsync());
            await db.Database.MigrateAsync(); // Only this test's disposable PostgreSQL database.
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var row = await db.PricingSimulations.SingleAsync(x => x.Id == id);
            Assert.Equal(4000, row.CurrentPrice); Assert.Equal(1000, row.UnitCost);
            Assert.Equal(300000, row.CurrentContribution); Assert.Equal(315000, row.ProposedContribution);
            Assert.Equal(15000, row.Difference); Assert.Equal(1, row.Version);
            Assert.Null(row.CostContext); Assert.Null(row.AdditionalDecisionCost);
            var preserved = await db.Database.SqlQuery<string>(
                $"""SELECT (to_jsonb(p) - 'AdditionalDecisionCost' - 'CostContext')::text AS "Value" FROM "PricingSimulations" p WHERE "Id"={id}""").SingleAsync();
            Assert.Equal(historicalRow, preserved); // Every original column, including both exact UTC timestamps.
        }
        logs.Exceptions.Clear();
        var list = await owner.GetAsync("/simulations");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains(id.ToString(), await list.Content.ReadAsStringAsync());
        var detail = await owner.GetAsync($"/simulations/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailHtml = WebUtility.HtmlDecode(await detail.Content.ReadAsStringAsync());
        Assert.Contains("315,000.00", detailHtml);
        Assert.Contains("Business-cost context was not captured", detailHtml);
        Assert.Empty(logs.Exceptions);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar-IQ")]
    [InlineData("ku-Arab-IQ")]
    public async Task History_MixedLegacyAndNewSnapshots_RendersEveryRecordAndPreservesTenantAndLifecycle(string culture)
    {
        using var owner = Browser();
        var email = await RegisterAndConfirm(owner); await Login(owner, email);
        var name = "History-" + Guid.NewGuid().ToString("N");
        var productFields = ProductFields(name, "10000"); productFields["UnitCost"] = "5000";
        await Submit(owner, "/products/create", productFields);
        var product = await FindProduct(name);
        var rows = new List<PricingSimulation>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == email);
            var now = DateTime.UtcNow;
            rows.Add(new(Guid.NewGuid(), product.BusinessId, product.Id, user.Id,
                4000, 1000, 4500, 100, 90, now, new PricingSimulationCalculator()));
            var entries = new[] { new LinkedCostEntry(Guid.NewGuid(), "Rent", BusinessCostCategory.Rent,
                3000000, CostCertainty.Estimated, UnitCostInclusion.No, 1, true) };
            foreach (var mode in new[] { "off", "context", "allocated" })
            {
                var settings = new CostLinkOptions(mode != "off", new(2026, 10, 1), mode == "allocated", 10, true);
                var pricing = new PricingSimulation(Guid.NewGuid(), product.BusinessId, product.Id, user.Id,
                    10000, 5000, 11000, 100, null, SalesPeriod.Month, 10, "IQD", name,
                    now, new PricingDecisionCalculator(new()), null);
                pricing.CaptureCostContext(SimulationCostCalculator.Calculate(settings, SalesPeriod.Month,
                    entries, false, 500000, [new("SameSales", 600000)]));
                rows.Add(pricing);
                var discount = PricingSimulation.CreateDiscount(Guid.NewGuid(), product.BusinessId, product.Id, user.Id,
                    new(10000, 5000, 10, 100, null, null), SalesPeriod.Month, "IQD", name, now, new(new()));
                discount.CaptureCostContext(SimulationCostCalculator.Calculate(settings, SalesPeriod.Month,
                    entries, false, discount.CurrentContribution, [new("Proposed contribution", null)]));
                rows.Add(discount);
                var delivery = PricingSimulation.CreateDelivery(Guid.NewGuid(), product.BusinessId, product.Id, user.Id,
                    10000, 5000, new(1, 10000, 25, 500, 0, null, SalesPeriod.Month, null, null, null),
                    "IQD", name, now, new(new()));
                delivery.CaptureCostContext(SimulationCostCalculator.Calculate(settings, SalesPeriod.Month,
                    entries, false, null, [new("Proposed contribution", null)]));
                rows.Add(delivery);
            }
            // Also exercise legitimate snapshots created before business-cost context existed.
            rows.Add(PricingSimulation.CreateDiscount(Guid.NewGuid(), product.BusinessId, product.Id, user.Id,
                new(10000, 5000, 10, 100, 140, 0), SalesPeriod.Week, "IQD", name, now, new(new())));
            rows.Add(PricingSimulation.CreateDelivery(Guid.NewGuid(), product.BusinessId, product.Id, user.Id,
                10000, 5000, new(1, 10000, 25, 500, 0, 0, SalesPeriod.Week, 100, 50, 10),
                "IQD", name, now, new(new())));
            // Exercise the real 20-row page boundary without losing any legacy entries.
            for (var i = 0; i < 9; i++)
                rows.Add(new(Guid.NewGuid(), product.BusinessId, product.Id, user.Id,
                    4000, 1000, 4500, 100, 90, now.AddDays(-1), new PricingSimulationCalculator()));
            db.PricingSimulations.AddRange(rows);
            await db.SaveChangesAsync();
            // Older JSON writers may omit nullable optional inputs rather than write null.
            // Remove only already-null inputs in these disposable fixtures; retain all results.
            var optionalDiscount = rows.First(x => x.DiscountSnapshot?.Inputs.ExpectedSalesQuantity is null
                && x.DiscountSnapshot is not null);
            var discountJson = JsonSerializer.SerializeToNode(optionalDiscount.DiscountSnapshot)!;
            discountJson["Inputs"]!.AsObject().Remove("CampaignCost");
            discountJson["Inputs"]!.AsObject().Remove("ExpectedSalesQuantity");
            var discountText = discountJson.ToJsonString();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "PricingSimulations" SET "DiscountSnapshot"={discountText} WHERE "Id"={optionalDiscount.Id}""");
            var optionalDelivery = rows.First(x => x.DeliverySnapshot is not null);
            var deliveryJson = JsonSerializer.SerializeToNode(optionalDelivery.DeliverySnapshot)!;
            foreach (var key in new[] { "FixedPeriodCost", "CurrentDirectOrders", "ExpectedDeliveryOrders", "TransferredOrders" })
                deliveryJson["Inputs"]!.AsObject().Remove(key);
            var deliveryText = deliveryJson.ToJsonString();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "PricingSimulations" SET "DeliverySnapshot"={deliveryText} WHERE "Id"={optionalDelivery.Id}""");
        }
        using var logs = new ExceptionLog();
        factory.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        await Submit(owner, "/culture", new() { ["culture"] = culture, ["returnUrl"] = "/simulations" }, "/simulations");
        var list = await owner.GetAsync("/simulations");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var html = await list.Content.ReadAsStringAsync();
        var secondPage = await owner.GetAsync("/simulations?page=2");
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        var secondHtml = await secondPage.Content.ReadAsStringAsync();
        Assert.Equal(20, Regex.Matches(html, "<article class=\"saved-simulation-card\">").Count);
        Assert.Single(Regex.Matches(secondHtml, "<article class=\"saved-simulation-card\">"));
        foreach (var row in rows)
        {
            Assert.Contains($"/simulations/{row.Id}", html + secondHtml);
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/simulations/{row.Id}")).StatusCode);
        }
        Assert.Empty(logs.Exceptions);
        string before;
        using (var scope = factory.Services.CreateScope())
            before = JsonSerializer.Serialize(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>()
                .PricingSimulations.AsNoTracking().Where(x => x.BusinessId == product.BusinessId).OrderBy(x => x.Id).ToArrayAsync());
        productFields["SellingPrice"] = "20000"; productFields["UnitCost"] = "15000";
        await Submit(owner, $"/products/{product.Id}/edit", productFields);
        await Submit(owner, "/business-costs/create", CostFields("Later unrelated expense", "42"));
        foreach (var row in rows)
            Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/simulations/{row.Id}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            Assert.Equal(before, JsonSerializer.Serialize(await db.PricingSimulations.AsNoTracking()
                .Where(x => x.BusinessId == product.BusinessId).OrderBy(x => x.Id).ToArrayAsync()));
            var archived = await db.PricingSimulations.SingleAsync(x => x.Id == rows[0].Id);
            archived.Archive(DateTime.UtcNow, archived.Version);
            var deleted = await db.PricingSimulations.SingleAsync(x => x.Id == rows[1].Id);
            deleted.Delete(DateTime.UtcNow, deleted.Version);
            await db.SaveChangesAsync();
        }
        var normal = await owner.GetStringAsync("/simulations");
        Assert.DoesNotContain($"/simulations/{rows[0].Id}", normal);
        Assert.DoesNotContain($"/simulations/{rows[1].Id}", normal);
        Assert.Contains($"/simulations/{rows[0].Id}", await owner.GetStringAsync("/simulations?includeArchived=true"));
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/simulations/{rows[0].Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/simulations/{rows[1].Id}")).StatusCode);
        using var other = Browser(); await Login(other, await RegisterAndConfirm(other));
        Assert.Contains("No saved simulations yet", await other.GetStringAsync("/simulations"));
        foreach (var row in rows)
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/simulations/{row.Id}")).StatusCode);
        var otherName = "Other-history-" + Guid.NewGuid().ToString("N");
        await Submit(other, "/products/create", ProductFields(otherName, "4000"));
        var otherProduct = await FindProduct(otherName);
        var otherId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var otherUser = await db.Users.SingleAsync(x => x.BusinessId == otherProduct.BusinessId);
            db.PricingSimulations.Add(new PricingSimulation(otherId, otherProduct.BusinessId, otherProduct.Id,
                otherUser.Id, 4000, 1000, 4500, 100, 90, DateTime.UtcNow, new PricingSimulationCalculator()));
            await db.SaveChangesAsync();
        }
        var otherHistory = await other.GetStringAsync("/simulations");
        Assert.Contains($"/simulations/{otherId}", otherHistory);
        Assert.All(rows, row => Assert.DoesNotContain($"/simulations/{row.Id}", otherHistory));
        Assert.DoesNotContain($"/simulations/{otherId}", await owner.GetStringAsync("/simulations?includeArchived=true"));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/simulations/{otherId}")).StatusCode);
    }

    [Theory]
    [InlineData("pricing", "off")] [InlineData("pricing", "context")] [InlineData("pricing", "allocated")]
    [InlineData("discount", "off")] [InlineData("discount", "context")] [InlineData("discount", "allocated")]
    [InlineData("delivery", "off")] [InlineData("delivery", "context")] [InlineData("delivery", "allocated")]
    public async Task History_ExplicitSaveListOpen_PreservesSnapshotsAfterProductAndExpenseEdits(string kind, string mode)
    {
        using var owner = Browser(); await Login(owner, await RegisterAndConfirm(owner));
        var name = "SavedJourney-" + Guid.NewGuid().ToString("N");
        var productFields = ProductFields(name, "10000"); productFields["UnitCost"] = "5000";
        await Submit(owner, "/products/create", productFields);
        var product = await FindProduct(name);
        await SeedLinkedCosts(product.BusinessId);
        var calculate = kind == "pricing" ? "/simulations/preview" : $"/simulations/{kind}";
        var formUrl = calculate + $"?productId={product.Id}";
        var fields = kind == "delivery" ? DeliveryFields(product.Id) : new Dictionary<string, string>
        {
            ["ProductId"] = product.Id.ToString(), ["CurrentSalesQuantity"] = "100",
            ["ExpectedSalesQuantity"] = "140", ["ComparisonPeriod"] = "Month"
        };
        fields["PreviewCurrentPrice"] = "10000"; fields["PreviewUnitCost"] = "5000";
        if (kind == "pricing") { fields["ProposedPrice"] = "11000"; fields["DeclinePercentage"] = "10"; }
        if (kind == "discount") fields["DiscountPercentage"] = "10"; // Campaign cost deliberately omitted.
        if (kind == "delivery") fields["DeliveryUnitPrice"] = "10000";
        if (mode != "off")
        {
            EnableCostLink(fields);
            if (mode == "context") fields["AllocateCosts"] = "false";
        }
        var preview = await Submit(owner, calculate, fields, formUrl);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        fields["CostBasis"] = CostInput(await preview.Content.ReadAsStringAsync(), "CostBasis");
        Assert.NotEmpty(fields["CostBasis"]);
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>()
                .PricingSimulations.AnyAsync(x => x.ProductId == product.Id));
        var save = await Submit(owner, calculate + "/save", fields, formUrl);
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        var location = save.Headers.Location!.OriginalString;
        var list = await owner.GetAsync("/simulations");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains($"href=\"{location}\"", await list.Content.ReadAsStringAsync());
        var detail = await owner.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var html = await detail.Content.ReadAsStringAsync();
        if (mode == "off") Assert.Contains("data-costs-excluded", html);
        else Assert.Contains("data-overhead-total", html);
        if (mode == "allocated") Assert.Contains("data-allocation-amount", html);
        else Assert.DoesNotContain("data-allocation-amount", html);

        string before;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            before = JsonSerializer.Serialize(await db.PricingSimulations.AsNoTracking().SingleAsync(x => x.ProductId == product.Id));
            var rent = await db.BusinessCosts.SingleAsync(x => x.BusinessId == product.BusinessId && x.Category == BusinessCostCategory.Rent);
            var update = CostFields(rent.Name, "12", rent.Version);
            Assert.Equal(HttpStatusCode.Redirect, (await Submit(owner, $"/business-costs/{rent.Id}/edit", update)).StatusCode);
        }
        productFields["UnitCost"] = "9000"; productFields["SellingPrice"] = "17000";
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(owner, $"/products/{product.Id}/edit", productFields)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/simulations")).StatusCode);
        var reopened = await owner.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        if (mode != "off") Assert.Contains("3,000,000.00", WebUtility.HtmlDecode(await reopened.Content.ReadAsStringAsync()));
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(before, JsonSerializer.Serialize(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>()
                .PricingSimulations.AsNoTracking().SingleAsync(x => x.ProductId == product.Id)));
        using var other = Browser(); await Login(other, await RegisterAndConfirm(other));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(location)).StatusCode);
        Assert.DoesNotContain(location, await other.GetStringAsync("/simulations"));
    }
}
