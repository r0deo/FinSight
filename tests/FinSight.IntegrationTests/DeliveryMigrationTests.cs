using System.Text.Json;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Services;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using FinSight.IntegrationTests.Infrastructure;

namespace FinSight.IntegrationTests;

public sealed class DeliveryMigrationTests
{
    [Fact]
    public async Task UpgradeFromDiscountSchema_PreservesDiscountSnapshot_AndAddsNullableDeliverySnapshot()
    {
        await using var container = new TestPostgresDatabase();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<FinSightDbContext>().UseNpgsql(container.GetConnectionString()).Options;
        var businessId = Guid.NewGuid(); var productId = Guid.NewGuid(); var userId = Guid.NewGuid(); var id = Guid.NewGuid();
        var discount = new DiscountSimulationCalculator(new()).Calculate(new(5000, 3000, 10, 100, 140, null));
        var json = JsonSerializer.Serialize(discount); var created = DateTime.UtcNow;
        await using (var previous = new FinSightDbContext(options))
        {
            await previous.GetService<IMigrator>().MigrateAsync("20261018141822_AddDiscountSimulationSnapshot");
            previous.Businesses.Add(new Business(businessId, "Upgrade test", "Duhok", "IQD", "test@example.com"));
            previous.Products.Add(new Product(productId, businessId, "Coffee", "Drinks", null, 5000, 3000));
            previous.Users.Add(new ApplicationUser(userId, businessId, "test@example.com"));
            await previous.SaveChangesAsync();
            // Today's entity mapping includes a new column, so seed the previous schema explicitly.
            await previous.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PricingSimulations" ("Id", "BusinessId", "ProductId", "CreatedByUserId",
                    "CurrentPrice", "UnitCost", "ProposedPrice", "CurrentSalesQuantity", "ExpectedSalesQuantity",
                    "CurrentContribution", "ProposedContribution", "Difference", "IsProposedPriceBelowCost",
                    "Recommendation", "CreatedAtUtc", "UpdatedAtUtc", "Version", "CalculationVersion",
                    "ComparisonPeriod", "CurrencySnapshot", "ProductNameSnapshot", "DiscountSnapshot")
                VALUES ({id}, {businessId}, {productId}, {userId}, 5000, 3000, 4500, 100, 140,
                    200000, 210000, 10000, FALSE, 'Recommended', {created}, {created}, 1, 1,
                    'Week', 'IQD', 'Coffee', {json})
                """);
        }
        await using var db = new FinSightDbContext(options);
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.False(db.Database.HasPendingModelChanges());
        var saved = await db.PricingSimulations.SingleAsync(x => x.Id == id);
        Assert.Equal(discount, saved.DiscountSnapshot); Assert.Null(saved.DeliverySnapshot);
        Assert.Equal(210000, saved.ProposedContribution); Assert.Equal(1, saved.Version);
        Assert.Null(saved.DiscountSnapshot!.Inputs.CampaignCost);
        db.PricingSimulations.Add(PricingSimulation.CreateDelivery(Guid.NewGuid(), businessId, productId, userId, 5000, 3000,
            new(1, 6000, 25, 500, 0, null, SalesPeriod.Week, null, null, null), "IQD", "Coffee", created, new(new())));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var delivery = await db.PricingSimulations.SingleAsync(x => x.Id != id);
        Assert.Equal(1000, delivery.DeliverySnapshot!.Result.DeliveryContributionPerOrder);
        Assert.Null(delivery.DeliverySnapshot.Inputs.CurrentDirectOrders); Assert.Null(delivery.DeliverySnapshot.Result.BaselineContribution);
        Assert.Null(delivery.DiscountSnapshot);
    }
}
