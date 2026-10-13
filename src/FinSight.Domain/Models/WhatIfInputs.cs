using FinSight.Domain.Enums;

namespace FinSight.Domain.Models;

public sealed record WhatIfInputs(
    decimal CurrentPrice,
    decimal CurrentUnitCost,
    int CurrentQuantity,
    CostShockInputMode CostInputMode,
    decimal? CostIncreasePercent,
    decimal? NewUnitCost,
    decimal SalesDeclinePercent,
    decimal? AlternativePrice,
    int? AlternativePriceQuantity,
    decimal? AlternativeSupplierCost,
    int? AlternativeSupplierQuantity);
