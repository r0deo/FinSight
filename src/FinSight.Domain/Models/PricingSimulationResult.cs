using FinSight.Domain.Enums;

namespace FinSight.Domain.Models;

public sealed record PricingSimulationResult(
    decimal CurrentContribution,
    decimal ProposedContribution,
    decimal Difference,
    bool IsBelowCost,
    Recommendation Recommendation);
