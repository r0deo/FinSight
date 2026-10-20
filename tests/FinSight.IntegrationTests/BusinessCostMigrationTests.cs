using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using FinSight.Domain.Services;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;
namespace FinSight.IntegrationTests;

public sealed class BusinessCostMigrationTests
{
    [Fact]
    public async Task UpgradeFromPreviousSchema_PreservesLegacyProductAndBothSimulationTypes()
    {
        await using var container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<FinSightDbContext>().UseNpgsql(container.GetConnectionString()).Options;
        var businessId = Guid.NewGuid(); var productId = Guid.NewGuid(); var userId = Guid.NewGuid();
        var pricingId = Guid.NewGuid(); var whatIfId = Guid.NewGuid();
        await using (var old = new FinSightDbContext(options))
        {
            await old.GetService<IMigrator>().MigrateAsync("20261013120101_AddWhatIfSimulations");
            old.Businesses.Add(new Business(businessId, "Legacy café", "Duhok", "IQD", "test@example.com"));
            old.Users.Add(new ApplicationUser(userId, businessId, "legacy@example.com"));
            await old.SaveChangesAsync();
            // Seed the historical schema explicitly; today's Product mapping has additional optional columns.
            await old.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Products" ("Id", "BusinessId", "Name", "Category", "Size", "SellingPrice", "UnitCost", "IsActive")
                VALUES ({productId}, {businessId}, 'Coffee', 'Drinks', NULL, 4000, 1000, TRUE)
                """);
            var created = DateTime.UtcNow;
            await old.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PricingSimulations" ("Id", "BusinessId", "ProductId", "CreatedByUserId",
                    "CurrentPrice", "UnitCost", "ProposedPrice", "CurrentSalesQuantity", "ExpectedSalesQuantity",
                    "CurrentContribution", "ProposedContribution", "Difference", "IsProposedPriceBelowCost",
                    "Recommendation", "CreatedAtUtc", "UpdatedAtUtc", "Version")
                VALUES ({pricingId}, {businessId}, {productId}, {userId}, 4000, 1000, 4500, 100, 90,
                    300000, 315000, 15000, FALSE, 'Recommended', {created}, {created}, 1)
                """);
            var legacyWhatIf = new WhatIfSimulation(whatIfId, businessId, productId, userId, "Coffee", "IQD",
                SalesPeriod.Month, new WhatIfInputs(4000,1000,100,CostShockInputMode.Percentage,20,null,10,null,null,null,null),
                created, new WhatIfCalculator(new PricingSimulationCalculator()));
            var inputs = System.Text.Json.JsonSerializer.Serialize(legacyWhatIf.Inputs);
            var result = System.Text.Json.JsonSerializer.Serialize(legacyWhatIf.Result);
            await old.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "WhatIfSimulations" ("Id", "BusinessId", "ProductId", "CreatedByUserId",
                    "ProductNameSnapshot", "CurrencySnapshot", "Period", "Inputs", "Result",
                    "CalculationVersion", "CreatedAtUtc", "UpdatedAtUtc", "Version")
                VALUES ({whatIfId}, {businessId}, {productId}, {userId}, 'Coffee', 'IQD', 'Month',
                    {inputs}, {result}, 1, {created}, {created}, 1)
                """);
            await old.GetService<IMigrator>().MigrateAsync("20261013163254_AddBusinessCosts");
            old.BusinessCosts.Add(new BusinessCost(Guid.NewGuid(), businessId, "Legacy rent", BusinessCostCategory.Rent,
                123, new(2026, 9, 1), CostCertainty.Actual, UnitCostInclusion.No, "Preserve this entry"));
            await old.SaveChangesAsync();
        }
        await using var db = new FinSightDbContext(options);
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        var legacyCost = await db.BusinessCosts.SingleAsync();
        Assert.Equal(123, legacyCost.Amount); Assert.Equal("Preserve this entry", legacyCost.Note);
        Assert.Equal(new DateOnly(2026, 9, 1), legacyCost.Month); Assert.Equal(1, legacyCost.Version);
        Assert.Empty(await db.BusinessCostMonths.ToListAsync()); // Existing months are not falsely declared complete.
        Assert.Equal(4000, (await db.Products.SingleAsync(x => x.Id == productId)).SellingPrice);
        Assert.Null((await db.Products.SingleAsync(x => x.Id == productId)).CostBreakdown);
        var pricing = await db.PricingSimulations.SingleAsync(x => x.Id == pricingId);
        Assert.Equal(300000, pricing.CurrentContribution); Assert.Equal(315000, pricing.ProposedContribution);
        Assert.Null(pricing.DiscountSnapshot);
        Assert.Null(pricing.DeliverySnapshot);
        Assert.Null(pricing.CostContext); Assert.Null(pricing.AdditionalDecisionCost);
        var whatIf = await db.WhatIfSimulations.SingleAsync(x => x.Id == whatIfId);
        Assert.Null(whatIf.CostContext);
        Assert.Equal(252000, whatIf.Result.NoActionContribution);
        Assert.Equal(1000, whatIf.Inputs.CurrentUnitCost);
        db.BusinessCosts.Add(new BusinessCost(Guid.NewGuid(), businessId, "Rent", BusinessCostCategory.Rent,
            500, new(2026,10,1), CostCertainty.Estimated, UnitCostInclusion.Unsure, null));
        await db.SaveChangesAsync();
        Assert.Equal(500, (await db.BusinessCosts.SingleAsync(x => x.Month == new DateOnly(2026, 10, 1))).Amount);
    }
}
