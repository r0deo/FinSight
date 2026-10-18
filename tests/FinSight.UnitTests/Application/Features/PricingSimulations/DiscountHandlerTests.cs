using FinSight.Application.Common.Exceptions;
using FinSight.Application.Features.PricingSimulations.Discounts;
using FinSight.Application.Features.PricingSimulations.Queries.GetSavedDecision;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Services;
using FinSight.Domain.Exceptions;
using FinSight.UnitTests.Application.Features.Products;

namespace FinSight.UnitTests.Application.Features.PricingSimulations;

public sealed class DiscountHandlerTests
{
    [Fact]
    public async Task PreviewAndSave_UseActiveTenantProduct_AndServerOwnedSnapshot()
    {
        var businessId = Guid.NewGuid(); var userId = Guid.NewGuid();
        var product = new Product(Guid.NewGuid(), businessId, "Coffee", "Drinks", null, 5000, 3000);
        var products = new StubProductRepository { ReadProduct = product };
        var simulations = new StubPricingSimulationRepository();
        var user = new StubCurrentUserContext(userId, businessId);
        var calculator = new DiscountSimulationCalculator(new());
        var preview = await new PreviewDiscountHandler(products, user, calculator).Handle(
            new(product.Id, 10, 100, 140, 0, SalesPeriod.Week), CancellationToken.None);
        Assert.Equal(10000, preview.Result.Difference); Assert.Equal(businessId, products.RequestedBusinessId);
        Assert.Equal(0, simulations.SaveChangesCount);
        var handler = new CreateDiscountHandler(products, simulations, new StubBusinessCurrencyReader(), user, calculator,
            new FixedTimeProvider(DateTimeOffset.UtcNow));
        var saved = await handler.Handle(new(product.Id, 10, 100, null, null, SalesPeriod.Month, 5000, 3000), CancellationToken.None);
        Assert.Equal(businessId, simulations.AddedSimulation!.BusinessId); Assert.Equal(userId, saved.CreatedByUserId);
        Assert.Null(saved.Recommendation); Assert.Null(saved.ProposedContribution); Assert.Null(saved.ExpectedSalesQuantity);
        Assert.Equal(SalesPeriod.Month, saved.ComparisonPeriod); Assert.Equal(1, simulations.SaveChangesCount);
        Assert.Equal("IQD", saved.CurrencySnapshot); Assert.Equal("Coffee", saved.ProductNameSnapshot);
        await Assert.ThrowsAsync<PricingSimulationConflictException>(() => handler.Handle(
            new(product.Id, 10, 100, 140, 0, SalesPeriod.Week, 4999, 3000), CancellationToken.None));
        Assert.Equal(1, simulations.SaveChangesCount);
    }

    [Fact]
    public async Task MissingTenantProductOrSimulation_IsNotFound()
    {
        var businessId = Guid.NewGuid(); var user = new StubCurrentUserContext(Guid.NewGuid(), businessId);
        var products = new StubProductRepository(); var simulations = new StubPricingSimulationRepository();
        await Assert.ThrowsAsync<NotFoundException>(() => new PreviewDiscountHandler(products, user, new(new())).Handle(
            new(Guid.NewGuid(), 10, 100, 140, 0, SalesPeriod.Week), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateDiscountHandler(simulations, user, new(new()), TimeProvider.System).Handle(
            new(Guid.NewGuid(), 10, 100, 140, 0, SalesPeriod.Week, 1), CancellationToken.None));
        Assert.Equal(businessId, products.RequestedBusinessId); Assert.Equal(businessId, simulations.RequestedBusinessId);
        Assert.Equal(0, simulations.SaveChangesCount);
    }

    [Fact]
    public async Task ReopenAndEdit_UseSavedSnapshotWithoutConsultingProduct()
    {
        var businessId = Guid.NewGuid(); var user = new StubCurrentUserContext(Guid.NewGuid(), businessId);
        var calculator = new DiscountSimulationCalculator(new());
        var simulation = PricingSimulation.CreateDiscount(Guid.NewGuid(), businessId, Guid.NewGuid(), user.UserId,
            new(5000, 3000, 10, 100, 140, 0), SalesPeriod.Week, "IQD", "Original", DateTime.UtcNow, calculator);
        var repository = new StubPricingSimulationRepository { ReadSimulation = simulation, TrackedSimulation = simulation };
        var read = await new GetSavedDecisionHandler(repository, new StubBusinessCurrencyReader(), user, new(new())).Handle(new(simulation.Id), CancellationToken.None);
        Assert.Null(read.Decision); Assert.Same(simulation.DiscountSnapshot, read.Simulation.DiscountSnapshot);
        var preview = await new PreviewSavedDiscountHandler(repository, user, calculator).Handle(
            new(simulation.Id, 10, 100, 120, 30000, SalesPeriod.Week, 1), CancellationToken.None);
        Assert.Equal(150000, preview.Result.ScenarioContribution); Assert.Equal(0, repository.SaveChangesCount);
        Assert.Equal(210000, simulation.ProposedContribution);
        await new UpdateDiscountHandler(repository, user, calculator, TimeProvider.System).Handle(
            new(simulation.Id, 10, 100, 120, 30000, SalesPeriod.Week, 1), CancellationToken.None);
        Assert.Equal(preview, simulation.DiscountSnapshot); Assert.Equal(1, repository.SaveChangesCount);
    }

    [Theory]
    [InlineData(0, 100, 140, 0, SalesPeriod.Week)]
    [InlineData(100, 100, 140, 0, SalesPeriod.Week)]
    [InlineData(10, 0, 140, 0, SalesPeriod.Week)]
    [InlineData(10, 100, -1, 0, SalesPeriod.Week)]
    [InlineData(10, 100, 140, -1, SalesPeriod.Week)]
    [InlineData(10, 100, 140, 0, (SalesPeriod)999)]
    public void Validators_RejectInvalidRequests(int discount, int current, int expected, int campaign, SalesPeriod period)
    {
        Assert.False(new PreviewDiscountValidator().Validate(new PreviewDiscountQuery(Guid.NewGuid(), discount, current, expected, campaign, period)).IsValid);
        Assert.False(new CreateDiscountValidator().Validate(new CreateDiscountCommand(Guid.NewGuid(), discount, current, expected, campaign, period, 5000, 3000)).IsValid);
        Assert.False(new UpdateDiscountValidator().Validate(new UpdateDiscountCommand(Guid.NewGuid(), discount, current, expected, campaign, period, 1)).IsValid);
    }
}
