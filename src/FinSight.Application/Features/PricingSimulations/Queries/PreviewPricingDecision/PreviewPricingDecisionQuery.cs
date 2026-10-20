using FinSight.Domain.Models;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingDecision;

public sealed record PreviewPricingDecisionQuery(
    Guid ProductId,
    decimal ProposedPrice,
    int CurrentSalesQuantity,
    int? ExpectedSalesQuantity,
    int DeclinePercentage) : IRequest<PricingDecisionPreviewResponse>
{
    public decimal? AdditionalDecisionCost { get; init; }
}

public sealed record PricingDecisionPreviewResponse(
    string ProductName,
    decimal CurrentPrice,
    decimal UnitCost,
    string Currency,
    PricingDecisionResult Decision);
