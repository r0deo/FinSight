using FinSight.Application.Common.Exceptions;
using FinSight.Application.Features.PricingSimulations.Delivery;
using FinSight.Application.Features.PricingSimulations.Queries.GetSavedDecision;
using FinSight.Domain.Entities;
using FinSight.Domain.Services;
using FinSight.Domain.Exceptions;
using FinSight.UnitTests.Application.Features.Products;
using FinSight.UnitTests.Domain.Services;

namespace FinSight.UnitTests.Application.Features.PricingSimulations;

public sealed class DeliveryHandlerTests
{
    [Fact]
    public async Task PreviewAndExplicitSave_UseAuthenticatedTenantAndServerFinancials()
    {
        var user = new StubCurrentUserContext(Guid.NewGuid(), Guid.NewGuid());
        var product = new Product(Guid.NewGuid(), user.BusinessId, "Coffee", "Drinks", null, 6000, 3000);
        var products = new StubProductRepository { ReadProduct = product };
        var simulations = new StubPricingSimulationRepository(); var calculator = new DeliverySimulationCalculator(new());
        var input = DeliverySimulationCalculatorTests.Inputs;
        var preview = await new PreviewDeliveryHandler(products, user, calculator).Handle(new(product.Id, input), default);
        Assert.Equal(0, preview.Result.Difference); Assert.Equal(user.BusinessId, products.RequestedBusinessId);
        Assert.Equal(0, simulations.SaveChangesCount);
        var handler = new CreateDeliveryHandler(products, simulations, new StubBusinessCurrencyReader(), user, calculator, TimeProvider.System);
        var saved = await handler.Handle(new(product.Id, input, 6000, 3000), default);
        Assert.Equal(user.BusinessId, simulations.AddedSimulation!.BusinessId); Assert.Equal(user.UserId, saved.CreatedByUserId);
        Assert.Equal(preview, saved.DeliverySnapshot); Assert.Equal("IQD", saved.CurrencySnapshot); Assert.Equal(1, simulations.SaveChangesCount);
        await Assert.ThrowsAsync<PricingSimulationConflictException>(() => handler.Handle(new(product.Id, input, 5999, 3000), default));
        Assert.Equal(1, simulations.SaveChangesCount);
    }

    [Fact]
    public async Task MissingTenantProductOrRecord_IsNotFound_AndDoesNotWrite()
    {
        var user = new StubCurrentUserContext(Guid.NewGuid(), Guid.NewGuid());
        var products = new StubProductRepository(); var records = new StubPricingSimulationRepository();
        await Assert.ThrowsAsync<NotFoundException>(() => new PreviewDeliveryHandler(products, user, new(new())).Handle(new(Guid.NewGuid(), DeliverySimulationCalculatorTests.Inputs), default));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateDeliveryHandler(records, user, new(new()), TimeProvider.System).Handle(new(Guid.NewGuid(), DeliverySimulationCalculatorTests.Inputs, 1), default));
        Assert.Equal(user.BusinessId, products.RequestedBusinessId); Assert.Equal(user.BusinessId, records.RequestedBusinessId);
        Assert.Equal(0, records.SaveChangesCount);
    }

    [Fact]
    public async Task ReopenAndEdit_DoNotConsultCurrentProduct_OrRecalculateOnRead()
    {
        var user = new StubCurrentUserContext(Guid.NewGuid(), Guid.NewGuid()); var calculator = new DeliverySimulationCalculator(new());
        var input = DeliverySimulationCalculatorTests.Inputs;
        var simulation = PricingSimulation.CreateDelivery(Guid.NewGuid(), user.BusinessId, Guid.NewGuid(), user.UserId,
            6000, 3000, input, "IQD", "Original", DateTime.UtcNow, calculator);
        var records = new StubPricingSimulationRepository { ReadSimulation = simulation, TrackedSimulation = simulation };
        var read = await new GetSavedDecisionHandler(records, new StubBusinessCurrencyReader(), user, new(new())).Handle(new(simulation.Id), default);
        Assert.Null(read.Decision); Assert.Same(simulation.DeliverySnapshot, read.Simulation.DeliverySnapshot);
        var updatedInput = input with { TransferredOrders = 0 };
        var preview = await new PreviewSavedDeliveryHandler(records, user, calculator).Handle(new(simulation.Id, updatedInput, 1), default);
        Assert.Equal(330000, preview.Result.ScenarioContribution); Assert.Equal(300000, simulation.ProposedContribution); Assert.Equal(0, records.SaveChangesCount);
        await new UpdateDeliveryHandler(records, user, calculator, TimeProvider.System).Handle(new(simulation.Id, updatedInput, 1), default);
        Assert.Equal(preview, simulation.DeliverySnapshot); Assert.Equal(1, records.SaveChangesCount);
    }

    [Fact]
    public void Validators_ValidateIdentityVersionAndDomainBounds_WithFieldNames()
    {
        var input = DeliverySimulationCalculatorTests.Inputs;
        Assert.False(new PreviewDeliveryValidator().Validate(new PreviewDeliveryQuery(Guid.Empty, input)).IsValid);
        Assert.False(new CreateDeliveryValidator().Validate(new CreateDeliveryCommand(Guid.NewGuid(), input, -1, 3000)).IsValid);
        Assert.False(new UpdateDeliveryValidator().Validate(new UpdateDeliveryCommand(Guid.NewGuid(), input, 0)).IsValid);
        var errors = new PreviewDeliveryValidator().Validate(new PreviewDeliveryQuery(Guid.NewGuid(), input with { TransferredOrders = 51 })).Errors;
        Assert.Contains(errors, x => x.PropertyName == "TransferredOrders" && x.ErrorCode == "DeliveryCalculation");
        Assert.True(new PreviewDeliveryValidator().Validate(new PreviewDeliveryQuery(Guid.NewGuid(), input with { TransferredOrders = null, FixedPeriodCost = null })).IsValid);
    }
}
