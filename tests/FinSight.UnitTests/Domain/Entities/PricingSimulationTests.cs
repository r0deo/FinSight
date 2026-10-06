using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Services;

namespace FinSight.UnitTests.Domain.Entities;

public class PricingSimulationTests
{
    [Fact]
    public void Constructor_CalculatesAndStoresSnapshot()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);

        // Act
        var simulation = CreateSimulation(createdAt);

        // Assert
        Assert.Equal(10m, simulation.CurrentPrice);
        Assert.Equal(4m, simulation.UnitCost);
        Assert.Equal(30m, simulation.CurrentContribution);
        Assert.Equal(48m, simulation.ProposedContribution);
        Assert.Equal(18m, simulation.Difference);
        Assert.Equal(Recommendation.Recommended, simulation.Recommendation);
        Assert.Equal(createdAt, simulation.CreatedAtUtc);
        Assert.Equal(createdAt, simulation.UpdatedAtUtc);
    }

    [Fact]
    public void Update_RecalculatesUsingOriginalProductSnapshot()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var updatedAt = createdAt.AddHours(1);
        var simulation = CreateSimulation(createdAt);

        // Act
        simulation.Update(5m, 5, 6, updatedAt, new PricingSimulationCalculator());

        // Assert
        Assert.Equal(10m, simulation.CurrentPrice);
        Assert.Equal(4m, simulation.UnitCost);
        Assert.Equal(30m, simulation.CurrentContribution);
        Assert.Equal(6m, simulation.ProposedContribution);
        Assert.Equal(-24m, simulation.Difference);
        Assert.Equal(Recommendation.NotRecommended, simulation.Recommendation);
        Assert.Equal(createdAt, simulation.CreatedAtUtc);
        Assert.Equal(updatedAt, simulation.UpdatedAtUtc);
    }

    private static PricingSimulation CreateSimulation(DateTime createdAt) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        10m,
        4m,
        12m,
        5,
        6,
        createdAt,
        new PricingSimulationCalculator());
}
