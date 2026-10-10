using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
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
        simulation.Update(
            5m,
            5,
            6,
            updatedAt,
            new PricingSimulationCalculator(),
            simulation.Version);

        // Assert
        Assert.Equal(10m, simulation.CurrentPrice);
        Assert.Equal(4m, simulation.UnitCost);
        Assert.Equal(30m, simulation.CurrentContribution);
        Assert.Equal(6m, simulation.ProposedContribution);
        Assert.Equal(-24m, simulation.Difference);
        Assert.Equal(Recommendation.NotRecommended, simulation.Recommendation);
        Assert.Equal(createdAt, simulation.CreatedAtUtc);
        Assert.Equal(updatedAt, simulation.UpdatedAtUtc);
        Assert.Equal(2, simulation.Version);
    }

    [Fact]
    public void ArchiveAndUnarchive_PreserveCalculationSnapshotAndAdvanceVersion()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var simulation = CreateSimulation(createdAt);

        // Act
        simulation.Archive(createdAt.AddMinutes(1), simulation.Version);
        var archivedDifference = simulation.Difference;
        simulation.Unarchive(createdAt.AddMinutes(2), simulation.Version);

        // Assert
        Assert.False(simulation.IsArchived);
        Assert.Null(simulation.ArchivedAtUtc);
        Assert.Equal(archivedDifference, simulation.Difference);
        Assert.Equal(3, simulation.Version);
    }

    [Fact]
    public void RepeatedArchiveOrUnarchive_ThrowsConflict()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var simulation = CreateSimulation(createdAt);
        simulation.Archive(createdAt.AddMinutes(1), simulation.Version);

        // Act
        var repeatedArchive = Record.Exception(() => simulation.Archive(
            createdAt.AddMinutes(2),
            simulation.Version));
        simulation.Unarchive(createdAt.AddMinutes(2), simulation.Version);
        var repeatedUnarchive = Record.Exception(() => simulation.Unarchive(
            createdAt.AddMinutes(3),
            simulation.Version));

        // Assert
        Assert.IsType<PricingSimulationConflictException>(repeatedArchive);
        Assert.IsType<PricingSimulationConflictException>(repeatedUnarchive);
    }

    [Fact]
    public void DeleteAndRestore_AtThirtyDayBoundary_Succeeds()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var simulation = CreateSimulation(createdAt);
        var deletedAt = createdAt.AddMinutes(1);
        simulation.Delete(deletedAt, simulation.Version);

        // Act
        simulation.Restore(
            deletedAt.Add(PricingSimulation.RestoreWindow),
            simulation.Version);

        // Assert
        Assert.False(simulation.IsDeleted);
        Assert.Null(simulation.DeletedAtUtc);
        Assert.Equal(3, simulation.Version);
        Assert.Throws<PricingSimulationConflictException>(() => simulation.Restore(
            deletedAt.Add(PricingSimulation.RestoreWindow),
            simulation.Version));
    }

    [Fact]
    public void Restore_AfterThirtyDays_ThrowsConflict()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var simulation = CreateSimulation(createdAt);
        var deletedAt = createdAt.AddMinutes(1);
        simulation.Delete(deletedAt, simulation.Version);

        // Act
        var act = () => simulation.Restore(
            deletedAt.Add(PricingSimulation.RestoreWindow).AddTicks(1),
            simulation.Version);

        // Assert
        Assert.Throws<PricingSimulationConflictException>(act);
        Assert.True(simulation.IsDeleted);
    }

    [Fact]
    public void IsEligibleForPermanentDeletion_WhenNotDeleted_ReturnsFalse()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var simulation = CreateSimulation(createdAt);

        // Act
        var isEligible = simulation.IsEligibleForPermanentDeletion(
            createdAt.Add(PricingSimulation.RestoreWindow).AddTicks(1));

        // Assert
        Assert.False(isEligible);
    }

    [Fact]
    public void IsEligibleForPermanentDeletion_AtRestoreBoundary_ReturnsFalse()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var deletedAt = createdAt.AddMinutes(1);
        var simulation = CreateSimulation(createdAt);
        simulation.Delete(deletedAt, simulation.Version);

        // Act
        var isEligible = simulation.IsEligibleForPermanentDeletion(
            deletedAt.Add(PricingSimulation.RestoreWindow));

        // Assert
        Assert.False(isEligible);
    }

    [Fact]
    public void IsEligibleForPermanentDeletion_AfterRestoreBoundary_ReturnsTrue()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var deletedAt = createdAt.AddMinutes(1);
        var simulation = CreateSimulation(createdAt);
        simulation.Delete(deletedAt, simulation.Version);

        // Act
        var isEligible = simulation.IsEligibleForPermanentDeletion(
            deletedAt.Add(PricingSimulation.RestoreWindow).AddTicks(1));

        // Assert
        Assert.True(isEligible);
    }

    [Fact]
    public void IsEligibleForPermanentDeletion_WithNonUtcTimestamp_ThrowsArgumentException()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var simulation = CreateSimulation(createdAt);
        var localTime = new DateTime(2026, 11, 5, 8, 0, 0, DateTimeKind.Local);

        // Act
        void Act() => simulation.IsEligibleForPermanentDeletion(localTime);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    [Fact]
    public void ArchivedOrDeletedSimulation_CannotBeEdited()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var archived = CreateSimulation(createdAt);
        archived.Archive(createdAt.AddMinutes(1), archived.Version);
        var deleted = CreateSimulation(createdAt);
        deleted.Delete(createdAt.AddMinutes(1), deleted.Version);

        // Act
        var updateArchived = () => archived.Update(
            15m, 5, 7, createdAt.AddMinutes(2),
            new PricingSimulationCalculator(), archived.Version);
        var updateDeleted = () => deleted.Update(
            15m, 5, 7, createdAt.AddMinutes(2),
            new PricingSimulationCalculator(), deleted.Version);

        // Assert
        Assert.Throws<PricingSimulationConflictException>(updateArchived);
        Assert.Throws<PricingSimulationConflictException>(updateDeleted);
    }

    [Fact]
    public void Mutation_WithStaleVersion_ThrowsConflict()
    {
        // Arrange
        var createdAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var simulation = CreateSimulation(createdAt);
        simulation.Archive(createdAt.AddMinutes(1), simulation.Version);

        // Act
        var act = () => simulation.Unarchive(createdAt.AddMinutes(2), 1);

        // Assert
        Assert.Throws<PricingSimulationConflictException>(act);
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
