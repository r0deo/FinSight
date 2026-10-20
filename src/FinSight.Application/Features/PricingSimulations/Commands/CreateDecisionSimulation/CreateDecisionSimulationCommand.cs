using FinSight.Domain.Enums;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.CreateDecisionSimulation;

public sealed record CreateDecisionSimulationCommand(
    Guid ProductId,
    decimal ProposedPrice,
    int CurrentSalesQuantity,
    int? ExpectedSalesQuantity,
    SalesPeriod ComparisonPeriod,
    int DeclinePercentage,
    decimal PreviewCurrentPrice,
    decimal PreviewUnitCost) : FinSight.Application.Features.BusinessCosts.CostLinkedRequest, IRequest<PricingSimulationResponse>
{
    public decimal? AdditionalDecisionCost { get; init; }
}
