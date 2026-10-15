using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Models;

namespace FinSight.Domain.Services;

public sealed class ProductCostCalculator
{
    public const int MaximumIngredients = 30;
    public const decimal MaximumQuantity = 1_000_000m;
    public const decimal MaximumInputCost = 999_999_999_999.99m;
    public const decimal MaximumStoredCost = 9_999_999_999_999_999.99m; // numeric(18,2)
    public ProductCostBreakdown Calculate(ProductCostInputs inputs)
    {
        if (inputs?.Ingredients is null || inputs.Ingredients.Count is < 1 or > MaximumIngredients)
            throw new ProductCostValidationException("Enter between 1 and 30 ingredients.");
        Quantity(inputs.UsableOutput);
        Cost(inputs.PackagingPerUnit); Cost(inputs.OtherVariableCostPerUnit);
        var lines = new List<ProductCostIngredient>();
        var amounts = new List<decimal>();
        decimal total = 0;
        try
        {
            foreach (var ingredient in inputs.Ingredients)
            {
                if (ingredient is null || string.IsNullOrWhiteSpace(ingredient.Name) || ingredient.Name.Trim().Length > 100)
                    throw new ProductCostValidationException("Each ingredient needs a name of at most 100 characters.");
                Quantity(ingredient.PackQuantity); Quantity(ingredient.UsedQuantity); Cost(ingredient.PackPrice);
                if (!Enum.IsDefined(ingredient.PackUnit) || !Enum.IsDefined(ingredient.UsedUnit))
                    throw new ProductCostValidationException("Choose a supported ingredient unit.");
                if (Group(ingredient.PackUnit) != Group(ingredient.UsedUnit))
                    throw new ProductCostValidationException("Use compatible units: mass with mass, volume with volume, or items with items.");
                var usedCost = checked(ingredient.PackPrice * (ingredient.UsedQuantity * Factor(ingredient.UsedUnit))
                    / (ingredient.PackQuantity * Factor(ingredient.PackUnit)));
                total = checked(total + usedCost);
                if (total > MaximumStoredCost) throw new OverflowException();
                lines.Add(ingredient with { Name = ingredient.Name.Trim() }); amounts.Add(usedCost);
            }
            var ingredientPerUnit = total / inputs.UsableOutput;
            var unitCost = Round(checked(ingredientPerUnit + (inputs.PackagingPerUnit ?? 0) + (inputs.OtherVariableCostPerUnit ?? 0)));
            if (unitCost > MaximumStoredCost) throw new OverflowException();
            return new(new(lines.AsReadOnly(), inputs.UsableOutput, inputs.PackagingPerUnit, inputs.OtherVariableCostPerUnit),
                new(amounts.AsReadOnly(), total, ingredientPerUnit, unitCost), 1);
        }
        catch (OverflowException) { throw new ProductCostValidationException("These quantities or costs are too large to calculate safely."); }
    }
    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static int Group(IngredientUnit unit) => unit switch { IngredientUnit.Gram or IngredientUnit.Kilogram => 0,
        IngredientUnit.Millilitre or IngredientUnit.Litre => 1, _ => 2 };
    private static decimal Factor(IngredientUnit unit) => unit is IngredientUnit.Kilogram or IngredientUnit.Litre ? 1000m : 1m;
    private static void Quantity(decimal quantity)
    {
        if (quantity <= 0 || quantity > MaximumQuantity || decimal.Round(quantity, 6) != quantity)
            throw new ProductCostValidationException("Quantities must be positive, at most 1,000,000, with no more than six decimal places.");
    }
    private static void Cost(decimal? cost)
    {
        if (cost.HasValue && (cost < 0 || cost > MaximumInputCost || Round(cost.Value) != cost))
            throw new ProductCostValidationException("Costs must be non-negative, at most 999,999,999,999.99, with no more than two decimal places.");
    }
}
