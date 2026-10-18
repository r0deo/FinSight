using FinSight.Application.Common.Exceptions;
using FinSight.Application.Features.PricingSimulations.Commands.ArchiveSimulation;
using FinSight.Application.Features.PricingSimulations.Commands.CreateSimulation;
using FinSight.Application.Features.PricingSimulations.Commands.CreateDecisionSimulation;
using FinSight.Application.Features.PricingSimulations.Commands.UpdateSimulation;
using FinSight.Application.Features.PricingSimulations.Queries.GetSimulationById;
using FinSight.Application.Features.PricingSimulations.Queries.GetSavedDecision;
using FinSight.Domain.Entities;
using FinSight.Domain.Services;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.UnitTests.Application.Features.Products;

namespace FinSight.UnitTests.Application.Features.PricingSimulations;

public class SavedPricingSimulationHandlerTests
{
    [Fact]
    public async Task CreateDecision_SavesUnknownExpectedAndPeriodForAuthenticatedBusiness()
    {
        var businessId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var product = new Product(Guid.NewGuid(), businessId, "Coffee", "Drinks", null, 4000, 1000);
        var products = new StubProductRepository { ReadProduct = product };
        var currencies = new StubBusinessCurrencyReader();
        var simulations = new StubPricingSimulationRepository();
        var handler = new CreateDecisionSimulationHandler(products, currencies, simulations,
            new StubCurrentUserContext(userId, businessId),
            new PricingDecisionCalculator(new PricingSimulationCalculator()),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(new CreateDecisionSimulationCommand(product.Id,
            5000, 33, null, SalesPeriod.Week, 10, 4000, 1000), CancellationToken.None);

        Assert.Equal(businessId, products.RequestedBusinessId);
        Assert.Equal(businessId, currencies.RequestedBusinessId);
        Assert.Equal(businessId, simulations.AddedSimulation!.BusinessId);
        Assert.Equal(userId, simulations.AddedSimulation.CreatedByUserId);
        Assert.Null(result.ExpectedSalesQuantity);
        Assert.Equal(SalesPeriod.Week, result.ComparisonPeriod);
        Assert.Equal(10, result.DeclinePercentage);
        Assert.Equal(99000, result.CurrentContribution);
        Assert.Equal(1, simulations.SaveChangesCount);
    }

    [Fact]
    public async Task CreateDecision_RejectsStaleProductSnapshotWithoutSaving()
    {
        var businessId = Guid.NewGuid();
        var product = new Product(Guid.NewGuid(), businessId, "Coffee", "Drinks", null, 4500, 1000);
        var simulations = new StubPricingSimulationRepository();
        var handler = new CreateDecisionSimulationHandler(
            new StubProductRepository { ReadProduct = product }, new StubBusinessCurrencyReader(),
            simulations, new StubCurrentUserContext(Guid.NewGuid(), businessId),
            new PricingDecisionCalculator(new PricingSimulationCalculator()),
            new FixedTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<PricingSimulationConflictException>(() => handler.Handle(
            new CreateDecisionSimulationCommand(product.Id, 5000, 33, null,
                SalesPeriod.Week, 10, 4000, 1000), CancellationToken.None));
        Assert.Null(simulations.AddedSimulation);
        Assert.Equal(0, simulations.SaveChangesCount);
    }

    [Fact]
    public async Task SavedDecision_ReadsTenantScopedSnapshotWithoutCurrentProduct()
    {
        var businessId = Guid.NewGuid();
        var simulation = new PricingSimulation(Guid.NewGuid(), businessId, Guid.NewGuid(),
            Guid.NewGuid(), 4000, 1000, 5000, 33, null, SalesPeriod.Week,
            10, "IQD", "Coffee", DateTime.UtcNow,
            new PricingDecisionCalculator(new PricingSimulationCalculator()));
        var repository = new StubPricingSimulationRepository { ReadSimulation = simulation };
        var handler = new GetSavedDecisionHandler(repository, new StubBusinessCurrencyReader(),
            new StubCurrentUserContext(Guid.NewGuid(), businessId),
            new PricingDecisionCalculator(new PricingSimulationCalculator()));

        var result = await handler.Handle(new GetSavedDecisionQuery(simulation.Id), CancellationToken.None);

        Assert.Equal(businessId, repository.RequestedBusinessId);
        Assert.NotNull(result.Decision);
        Assert.Equal(25, result.Decision.MinimumSalesToMaintainContribution);
        Assert.Equal("IQD", result.Currency);
        Assert.Null(result.Decision.ExpectedScenario);
    }

    [Fact]
    public async Task Create_SavesCalculatedSnapshotForAuthenticatedBusinessAndUser()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var product = new Product(
            Guid.NewGuid(), businessId, "Coffee", "Drinks", null, 10m, 4m);
        var productRepository = new StubProductRepository { ReadProduct = product };
        var simulationRepository = new StubPricingSimulationRepository();
        var handler = new CreateSimulationHandler(
            productRepository,
            simulationRepository,
            new StubCurrentUserContext(userId, businessId),
            new PricingSimulationCalculator(),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero)));

        // Act
        var result = await handler.Handle(
            new CreateSimulationCommand(product.Id, 12m, 5, 6),
            CancellationToken.None);

        // Assert
        Assert.Equal(businessId, productRepository.RequestedBusinessId);
        Assert.NotNull(simulationRepository.AddedSimulation);
        Assert.Equal(businessId, simulationRepository.AddedSimulation.BusinessId);
        Assert.Equal(userId, simulationRepository.AddedSimulation.CreatedByUserId);
        Assert.Equal(10m, result.CurrentPrice);
        Assert.Equal(4m, result.UnitCost);
        Assert.Equal(18m, result.Difference);
        Assert.Equal(1, simulationRepository.SaveChangesCount);
    }

    [Fact]
    public async Task Update_RecalculatesFromSavedCurrentPriceAndUnitCost()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var simulation = CreateSimulation(businessId);
        var repository = new StubPricingSimulationRepository
        {
            TrackedSimulation = simulation
        };
        var handler = new UpdateSimulationHandler(
            repository,
            new StubCurrentUserContext(Guid.NewGuid(), businessId),
            new PricingSimulationCalculator(),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero)));

        // Act
        await handler.Handle(
            new UpdateSimulationCommand(simulation.Id, 5m, 5, 6, simulation.Version),
            CancellationToken.None);

        // Assert
        Assert.Equal(businessId, repository.RequestedBusinessId);
        Assert.Equal(10m, simulation.CurrentPrice);
        Assert.Equal(4m, simulation.UnitCost);
        Assert.Equal(30m, simulation.CurrentContribution);
        Assert.Equal(6m, simulation.ProposedContribution);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task GetById_WhenOutsideAuthenticatedBusiness_ThrowsNotFoundException()
    {
        // Arrange
        var simulationId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var repository = new StubPricingSimulationRepository();
        var handler = new GetSimulationByIdHandler(
            repository,
            new StubCurrentUserContext(Guid.NewGuid(), businessId));

        // Act
        var act = () => handler.Handle(
            new GetSimulationByIdQuery(simulationId),
            CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Equal(businessId, repository.RequestedBusinessId);
    }

    [Fact]
    public async Task Archive_ChangesStateForAuthenticatedBusinessAndReturnsNewVersion()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var simulation = CreateSimulation(businessId);
        var repository = new StubPricingSimulationRepository
        {
            TrackedSimulation = simulation
        };
        var handler = new ArchiveSimulationHandler(
            repository,
            new StubCurrentUserContext(Guid.NewGuid(), businessId),
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero)));

        // Act
        var result = await handler.Handle(
            new ArchiveSimulationCommand(simulation.Id, simulation.Version),
            CancellationToken.None);

        // Assert
        Assert.True(simulation.IsArchived);
        Assert.Equal(2, result.Version);
        Assert.Equal(businessId, repository.RequestedBusinessId);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    private static PricingSimulation CreateSimulation(Guid businessId) => new(
        Guid.NewGuid(),
        businessId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        10m,
        4m,
        12m,
        5,
        6,
        new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc),
        new PricingSimulationCalculator());
}
