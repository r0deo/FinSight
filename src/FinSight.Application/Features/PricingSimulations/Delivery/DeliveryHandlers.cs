using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Models;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Services;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Delivery;

public sealed class PreviewDeliveryHandler(IProductRepository products, ICurrentUserContext user,
    DeliverySimulationCalculator calculator) : IRequestHandler<PreviewDeliveryQuery, DeliverySimulationSnapshot>
{
    public async Task<DeliverySimulationSnapshot> Handle(PreviewDeliveryQuery request, CancellationToken ct)
    {
        var product = await products.GetActiveByIdAsync(request.ProductId, user.BusinessId, ct)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);
        return DeliveryValidation.Run(() => calculator.Calculate(product.SellingPrice, product.UnitCost, request.Inputs));
    }
}
public sealed class PreviewSavedDeliveryHandler(IPricingSimulationRepository simulations, ICurrentUserContext user,
    DeliverySimulationCalculator calculator) : IRequestHandler<PreviewSavedDeliveryQuery, DeliverySimulationSnapshot>
{
    public async Task<DeliverySimulationSnapshot> Handle(PreviewSavedDeliveryQuery request, CancellationToken ct)
    {
        var simulation = await simulations.GetByIdAsync(request.SimulationId, user.BusinessId, ct);
        if (simulation?.DeliverySnapshot is null) throw new NotFoundException(nameof(PricingSimulation), request.SimulationId);
        if (simulation.IsArchived || simulation.Version != request.Version)
            throw new PricingSimulationConflictException("The simulation is archived or has changed. Reload it before editing.");
        return DeliveryValidation.Run(() => calculator.Calculate(simulation.CurrentPrice, simulation.UnitCost, request.Inputs));
    }
}
public sealed class CreateDeliveryHandler(IProductRepository products, IPricingSimulationRepository simulations,
    IBusinessCurrencyReader currencies, ICurrentUserContext user, DeliverySimulationCalculator calculator,
    TimeProvider clock) : IRequestHandler<CreateDeliveryCommand, PricingSimulationResponse>
{
    public async Task<PricingSimulationResponse> Handle(CreateDeliveryCommand request, CancellationToken ct)
    {
        var businessId = user.BusinessId;
        var product = await products.GetActiveByIdAsync(request.ProductId, businessId, ct)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);
        if (product.SellingPrice != request.PreviewCurrentPrice || product.UnitCost != request.PreviewUnitCost)
            throw new PricingSimulationConflictException("The product price or cost changed since this preview. Calculate again before saving.");
        var currency = await currencies.GetCurrencyAsync(businessId, ct)
            ?? throw new InvalidOperationException("The current business is unavailable.");
        var simulation = DeliveryValidation.Run(() => PricingSimulation.CreateDelivery(Guid.NewGuid(), businessId, product.Id,
            user.UserId, product.SellingPrice, product.UnitCost, request.Inputs, currency, product.Name,
            clock.GetUtcNow().UtcDateTime, calculator));
        await simulations.AddAsync(simulation, ct); await simulations.SaveChangesAsync(ct);
        return PricingSimulationResponse.FromSimulation(simulation);
    }
}
public sealed class UpdateDeliveryHandler(IPricingSimulationRepository simulations, ICurrentUserContext user,
    DeliverySimulationCalculator calculator, TimeProvider clock) : IRequestHandler<UpdateDeliveryCommand, PricingSimulationVersionResponse>
{
    public async Task<PricingSimulationVersionResponse> Handle(UpdateDeliveryCommand request, CancellationToken ct)
    {
        var simulation = await simulations.GetByIdForUpdateAsync(request.SimulationId, user.BusinessId, ct);
        if (simulation is null || simulation.IsDeleted || simulation.DeliverySnapshot is null)
            throw new NotFoundException(nameof(PricingSimulation), request.SimulationId);
        DeliveryValidation.Run(() => { simulation.UpdateDelivery(request.Inputs, clock.GetUtcNow().UtcDateTime, request.Version, calculator); return true; });
        await simulations.SaveChangesAsync(ct);
        return new(simulation.Version);
    }
}
