using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Application.Features.PricingSimulations;

public sealed record PricingSimulationResponse(
    Guid Id,
    Guid ProductId,
    Guid CreatedByUserId,
    decimal CurrentPrice,
    decimal UnitCost,
    decimal ProposedPrice,
    int CurrentSalesQuantity,
    int ExpectedSalesQuantity,
    decimal CurrentContribution,
    decimal ProposedContribution,
    decimal Difference,
    bool IsProposedPriceBelowCost,
    Recommendation Recommendation,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static PricingSimulationResponse FromSimulation(PricingSimulation simulation) => new(
        simulation.Id,
        simulation.ProductId,
        simulation.CreatedByUserId,
        simulation.CurrentPrice,
        simulation.UnitCost,
        simulation.ProposedPrice,
        simulation.CurrentSalesQuantity,
        simulation.ExpectedSalesQuantity,
        simulation.CurrentContribution,
        simulation.ProposedContribution,
        simulation.Difference,
        simulation.IsProposedPriceBelowCost,
        simulation.Recommendation,
        simulation.CreatedAtUtc,
        simulation.UpdatedAtUtc);
}
