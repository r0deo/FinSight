using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Models;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Services;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Discounts;

public sealed class PreviewDiscountHandler(IProductRepository products, ICurrentUserContext user,
    DiscountSimulationCalculator calculator) : IRequestHandler<PreviewDiscountQuery, DiscountSimulationSnapshot>
{
    public async Task<DiscountSimulationSnapshot> Handle(PreviewDiscountQuery request, CancellationToken ct)
    {
        var product = await products.GetActiveByIdAsync(request.ProductId, user.BusinessId, ct)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);
        return DiscountValidation.Run(() => calculator.Calculate(new(product.SellingPrice, product.UnitCost,
            request.DiscountPercentage, request.CurrentSalesQuantity, request.ExpectedSalesQuantity, request.CampaignCost)));
    }
}

public sealed class PreviewSavedDiscountHandler(IPricingSimulationRepository simulations, ICurrentUserContext user,
    DiscountSimulationCalculator calculator) : IRequestHandler<PreviewSavedDiscountQuery, DiscountSimulationSnapshot>
{
    public async Task<DiscountSimulationSnapshot> Handle(PreviewSavedDiscountQuery request, CancellationToken ct)
    {
        var simulation = await simulations.GetByIdAsync(request.SimulationId, user.BusinessId, ct);
        if (simulation?.DiscountSnapshot is null) throw new NotFoundException(nameof(PricingSimulation), request.SimulationId);
        if (simulation.IsArchived || simulation.Version != request.Version)
            throw new PricingSimulationConflictException("The simulation is archived or has changed. Reload it before editing.");
        return DiscountValidation.Run(() => calculator.Calculate(new(simulation.CurrentPrice, simulation.UnitCost,
            request.DiscountPercentage, request.CurrentSalesQuantity, request.ExpectedSalesQuantity, request.CampaignCost)));
    }
}

public sealed class CreateDiscountHandler(IProductRepository products, IPricingSimulationRepository simulations,
    IBusinessCurrencyReader currencies, ICurrentUserContext user, DiscountSimulationCalculator calculator,
    TimeProvider clock) : IRequestHandler<CreateDiscountCommand, PricingSimulationResponse>
{
    public async Task<PricingSimulationResponse> Handle(CreateDiscountCommand request, CancellationToken ct)
    {
        var businessId = user.BusinessId;
        var product = await products.GetActiveByIdAsync(request.ProductId, businessId, ct)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);
        if (product.SellingPrice != request.PreviewCurrentPrice || product.UnitCost != request.PreviewUnitCost)
            throw new PricingSimulationConflictException("The product price or cost changed since this preview. Calculate again before saving.");
        var currency = await currencies.GetCurrencyAsync(businessId, ct)
            ?? throw new InvalidOperationException("The current business is unavailable.");
        var simulation = DiscountValidation.Run(() => PricingSimulation.CreateDiscount(Guid.NewGuid(), businessId,
            product.Id, user.UserId, new(product.SellingPrice, product.UnitCost, request.DiscountPercentage,
                request.CurrentSalesQuantity, request.ExpectedSalesQuantity, request.CampaignCost),
            request.ComparisonPeriod, currency, product.Name, clock.GetUtcNow().UtcDateTime, calculator));
        await simulations.AddAsync(simulation, ct);
        await simulations.SaveChangesAsync(ct);
        return PricingSimulationResponse.FromSimulation(simulation);
    }
}

public sealed class UpdateDiscountHandler(IPricingSimulationRepository simulations, ICurrentUserContext user,
    DiscountSimulationCalculator calculator, TimeProvider clock)
    : IRequestHandler<UpdateDiscountCommand, PricingSimulationVersionResponse>
{
    public async Task<PricingSimulationVersionResponse> Handle(UpdateDiscountCommand request, CancellationToken ct)
    {
        var simulation = await simulations.GetByIdForUpdateAsync(request.SimulationId, user.BusinessId, ct);
        if (simulation is null || simulation.IsDeleted || simulation.DiscountSnapshot is null)
            throw new NotFoundException(nameof(PricingSimulation), request.SimulationId);
        DiscountValidation.Run(() =>
        {
            simulation.UpdateDiscount(request.DiscountPercentage, request.CurrentSalesQuantity,
                request.ExpectedSalesQuantity, request.CampaignCost, request.ComparisonPeriod,
                clock.GetUtcNow().UtcDateTime, request.Version, calculator);
            return true;
        });
        await simulations.SaveChangesAsync(ct);
        return new(simulation.Version);
    }
}
