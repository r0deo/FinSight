using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Services;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.CreateDecisionSimulation;

public sealed class CreateDecisionSimulationHandler(
    IProductRepository products,
    IBusinessCurrencyReader currencies,
    IPricingSimulationRepository simulations,
    ICurrentUserContext currentUser,
    PricingDecisionCalculator calculator,
    TimeProvider clock, FinSight.Application.Features.BusinessCosts.SimulationCostContextService? costContext = null) : IRequestHandler<CreateDecisionSimulationCommand, PricingSimulationResponse>
{
    public async Task<PricingSimulationResponse> Handle(
        CreateDecisionSimulationCommand request, CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId;
        var product = await products.GetActiveByIdAsync(request.ProductId, businessId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);
        if (product.SellingPrice != request.PreviewCurrentPrice || product.UnitCost != request.PreviewUnitCost)
            throw new PricingSimulationConflictException(
                "The product price or cost changed since this preview. Calculate again before saving.");
        var currency = await currencies.GetCurrencyAsync(businessId, cancellationToken)
            ?? throw new InvalidOperationException("The current business is unavailable.");
        var simulation = new PricingSimulation(Guid.NewGuid(), businessId, product.Id,
            currentUser.UserId, product.SellingPrice, product.UnitCost, request.ProposedPrice,
            request.CurrentSalesQuantity, request.ExpectedSalesQuantity, request.ComparisonPeriod,
            request.DeclinePercentage, currency, product.Name, clock.GetUtcNow().UtcDateTime, calculator, request.AdditionalDecisionCost);
        var decision = calculator.Calculate(product.SellingPrice, request.ProposedPrice, product.UnitCost,
            request.CurrentSalesQuantity, request.ExpectedSalesQuantity, request.DeclinePercentage, request.AdditionalDecisionCost);
        simulation.CaptureCostContext(await FinSight.Application.Features.BusinessCosts.SimulationCostContextService.Capture(costContext, request.CostLink, request.ComparisonPeriod,
            decision.CurrentContribution, decision.Scenarios.Select(x => new FinSight.Domain.Models.ContributionOption(x.Kind.ToString(), x.Contribution)).ToArray(),
            cancellationToken, request.CostBasis, true));
        await simulations.AddAsync(simulation, cancellationToken);
        await simulations.SaveChangesAsync(cancellationToken);
        return PricingSimulationResponse.FromSimulation(simulation);
    }
}
