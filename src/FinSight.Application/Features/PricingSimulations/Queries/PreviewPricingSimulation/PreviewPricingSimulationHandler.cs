using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Services;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingSimulation;

public sealed class PreviewPricingSimulationHandler(
    IProductRepository productRepository,
    ICurrentUserContext currentUserContext,
    PricingSimulationCalculator calculator)
    : IRequestHandler<PreviewPricingSimulationQuery, PreviewPricingSimulationResponse>
{
    public async Task<PreviewPricingSimulationResponse> Handle(
        PreviewPricingSimulationQuery request,
        CancellationToken cancellationToken)
    {
        var businessId = currentUserContext.BusinessId;
        var product = await productRepository.GetByIdAsync(
            request.ProductId,
            businessId,
            cancellationToken);

        if (product is null)
        {
            throw new NotFoundException(nameof(Product), request.ProductId);
        }

        var result = calculator.Calculate(
            product.SellingPrice,
            request.ProposedPrice,
            product.UnitCost,
            request.CurrentSalesQuantity,
            request.ExpectedSalesQuantity);

        return new PreviewPricingSimulationResponse(
            result.CurrentContribution,
            result.ProposedContribution,
            result.Difference,
            result.Recommendation,
            result.IsBelowCost);
    }
}
