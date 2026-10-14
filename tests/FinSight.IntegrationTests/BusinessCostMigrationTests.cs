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
            old.Products.Add(new Product(productId, businessId, "Coffee", "Drinks", null, 4000, 1000));
            old.Users.Add(new ApplicationUser(userId, businessId, "legacy@example.com"));
            old.PricingSimulations.Add(new PricingSimulation(pricingId, businessId, productId, userId,
                4000, 1000, 4500, 100, 90, DateTime.UtcNow, new PricingSimulationCalculator()));
            old.WhatIfSimulations.Add(new WhatIfSimulation(whatIfId, businessId, productId, userId, "Coffee", "IQD",
                SalesPeriod.Month, new WhatIfInputs(4000,1000,100,CostShockInputMode.Percentage,20,null,10,null,null,null,null),
                DateTime.UtcNow, new WhatIfCalculator(new PricingSimulationCalculator())));
            await old.SaveChangesAsync();
        }
        await using var db = new FinSightDbContext(options);
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(4000, (await db.Products.SingleAsync(x => x.Id == productId)).SellingPrice);
        var pricing = await db.PricingSimulations.SingleAsync(x => x.Id == pricingId);
        Assert.Equal(300000, pricing.CurrentContribution); Assert.Equal(315000, pricing.ProposedContribution);
        var whatIf = await db.WhatIfSimulations.SingleAsync(x => x.Id == whatIfId);
        Assert.Equal(252000, whatIf.Result.NoActionContribution);
        Assert.Equal(1000, whatIf.Inputs.CurrentUnitCost);
        db.BusinessCosts.Add(new BusinessCost(Guid.NewGuid(), businessId, "Rent", BusinessCostCategory.Rent,
            500, new(2026,10,1), CostCertainty.Estimated, UnitCostInclusion.Unsure, null));
        await db.SaveChangesAsync();
        Assert.Equal(500, (await db.BusinessCosts.SingleAsync()).Amount);
    }
}
