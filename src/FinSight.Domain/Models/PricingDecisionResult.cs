using FinSight.Domain.Enums;

namespace FinSight.Domain.Models;

public sealed record PricingScenarioResult(
    PricingScenarioKind Kind,
    int Quantity,
    decimal Revenue,
    decimal TotalUnitCosts,
    decimal Contribution,
    decimal Difference);

public sealed record PricingDecisionResult(
    decimal CurrentContribution,
    MaintenanceThresholdStatus ThresholdStatus,
    int? MinimumSalesToMaintainContribution,
    int? QuantityChange,
    decimal? QuantityChangePercent,
    bool IsProposedPriceBelowCost,
    IReadOnlyList<PricingScenarioResult> Scenarios)
{
    public PricingScenarioResult? ExpectedScenario =>
        Scenarios.FirstOrDefault(scenario => scenario.Kind == PricingScenarioKind.ExpectedSales);
}
