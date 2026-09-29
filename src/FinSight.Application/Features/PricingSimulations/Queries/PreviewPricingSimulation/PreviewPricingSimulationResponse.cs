using FinSight.Domain.Enums;

namespace FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingSimulation;

public sealed record PreviewPricingSimulationResponse(
    decimal CurrentContribution,
    decimal ProposedContribution,
    decimal Difference,
    Recommendation Recommendation,
    bool IsProposedPriceBelowCost);
