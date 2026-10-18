using FinSight.Domain.Enums;

namespace FinSight.Domain.Models;

public sealed record DiscountInputs(decimal CurrentPrice, decimal UnitCost, decimal DiscountPercentage,
    int CurrentSalesQuantity, int? ExpectedSalesQuantity, decimal? CampaignCost);

public sealed record DiscountResult(decimal DiscountedPrice, decimal CurrentContribution,
    decimal ContributionPerUnit, decimal? ScenarioContribution, decimal? Difference,
    MaintenanceThresholdStatus ThresholdStatus, int? MinimumUnits, int? AdditionalUnits,
    decimal? RequiredIncreasePercentage, bool IsBelowCost);

// Persist the entire immutable answer, not a reference to today's product or calculation rules.
public sealed record DiscountSimulationSnapshot(DiscountInputs Inputs, DiscountResult Result, int CalculationVersion = 1);
