using FinSight.Domain.Enums;

namespace FinSight.Domain.Models;

public sealed record DeliveryAssumptions(int UnitsPerOrder, decimal DeliveryUnitPrice, decimal CommissionPercentage,
    decimal PackagingPerOrder, decimal OtherChargesPerOrder, decimal? FixedPeriodCost, SalesPeriod ComparisonPeriod,
    int? CurrentDirectOrders, int? ExpectedDeliveryOrders, int? TransferredOrders);

public sealed record DeliveryResult(decimal DirectContributionPerOrder, decimal ProductSubtotal,
    decimal CommissionPerOrder, decimal RecordedCostPerOrder, decimal DeliveryContributionPerOrder,
    decimal? BaselineContribution, decimal? ScenarioContribution, decimal? Difference,
    int? RemainingDirectOrders, int? NewDeliveryOrders);

public sealed record DeliverySimulationSnapshot(decimal CurrentPrice, decimal UnitCost,
    DeliveryAssumptions Inputs, DeliveryResult Result, int CalculationVersion = 1,
    string CommissionBasis = "ProductSubtotal", bool DirectDemandOtherwiseUnchanged = true);
