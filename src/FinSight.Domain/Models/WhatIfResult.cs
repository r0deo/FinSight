using FinSight.Domain.Enums;

namespace FinSight.Domain.Models;

public sealed record WhatIfOptionResult(
    WhatIfOptionKind Kind,
    decimal SellingPrice,
    decimal UnitCost,
    int Quantity,
    decimal Revenue,
    decimal TotalUnitCosts,
    decimal Contribution,
    decimal DifferenceFromBaseline,
    decimal DifferenceFromNoAction,
    bool IsBelowCost,
    MaintenanceThresholdStatus ThresholdStatus,
    int? MinimumUnitsToMaintainBaseline);

public sealed record WhatIfResult(
    decimal BaselineContribution,
    decimal ShockedUnitCost,
    int ShockedQuantity,
    decimal NoActionContribution,
    decimal DifferenceFromBaseline,
    IReadOnlyList<WhatIfOptionResult> Options);
