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
    decimal PreviewUnitCost) : IRequest<PricingSimulationResponse>;
