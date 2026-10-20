using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Services;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingDecision;

public sealed class PreviewPricingDecisionHandler(
    IProductRepository products,
    IBusinessCurrencyReader currencies,
    ICurrentUserContext currentUser,
    PricingDecisionCalculator calculator)
    : IRequestHandler<PreviewPricingDecisionQuery, PricingDecisionPreviewResponse>
{
    public async Task<PricingDecisionPreviewResponse> Handle(
        PreviewPricingDecisionQuery request, CancellationToken cancellationToken)
    {
        var businessId = currentUser.BusinessId;
        var product = await products.GetActiveByIdAsync(request.ProductId, businessId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);
        var currency = await currencies.GetCurrencyAsync(businessId, cancellationToken)
            ?? throw new InvalidOperationException("The current business is unavailable.");
        var decision = calculator.Calculate(product.SellingPrice, request.ProposedPrice,
            product.UnitCost, request.CurrentSalesQuantity, request.ExpectedSalesQuantity,
            request.DeclinePercentage, request.AdditionalDecisionCost);
        return new PricingDecisionPreviewResponse(product.Name, product.SellingPrice,
            product.UnitCost, currency, decision);
    }
}
