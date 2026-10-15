using FinSight.Domain.Enums;

namespace FinSight.Domain.Models;

public sealed record ProductCostIngredient(string Name, decimal PackQuantity, IngredientUnit PackUnit,
    decimal PackPrice, decimal UsedQuantity, IngredientUnit UsedUnit);
public sealed record ProductCostInputs(IReadOnlyList<ProductCostIngredient> Ingredients, decimal UsableOutput,
    decimal? PackagingPerUnit, decimal? OtherVariableCostPerUnit);
public sealed record ProductCostResult(IReadOnlyList<decimal> IngredientCosts, decimal TotalIngredientCost,
    decimal IngredientCostPerUnit, decimal EstimatedUnitCost);
public sealed record ProductCostBreakdown(ProductCostInputs Inputs, ProductCostResult Result, int CalculationVersion);
