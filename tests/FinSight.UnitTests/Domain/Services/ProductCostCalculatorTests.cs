using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Models;
using FinSight.Domain.Services;

namespace FinSight.UnitTests.Domain.Services;

public sealed class ProductCostCalculatorTests
{
    private readonly ProductCostCalculator calculator = new();
    private static ProductCostInputs Batch() => new([new("Ingredients", 1, IngredientUnit.Item, 12000, 1, IngredientUnit.Item)], 10, 200, 0);

    [Fact]
    public void ReferenceBatch_Calculates1400_AndRetainsExplicitZero()
    {
        var result = calculator.Calculate(Batch());
        Assert.Equal(12000, result.Result.TotalIngredientCost); Assert.Equal(1200, result.Result.IngredientCostPerUnit);
        Assert.Equal(1400, result.Result.EstimatedUnitCost); Assert.Equal(0, result.Inputs.OtherVariableCostPerUnit);
    }
    [Theory]
    [InlineData(1, IngredientUnit.Kilogram, 200, IngredientUnit.Gram, 2000)]
    [InlineData(1000, IngredientUnit.Gram, 0.2, IngredientUnit.Kilogram, 2000)]
    [InlineData(1, IngredientUnit.Litre, 200, IngredientUnit.Millilitre, 2000)]
    [InlineData(1000, IngredientUnit.Millilitre, 0.2, IngredientUnit.Litre, 2000)]
    [InlineData(10, IngredientUnit.Item, 2, IngredientUnit.Item, 2000)]
    [InlineData(10, IngredientUnit.Item, 0.5, IngredientUnit.Item, 500)]
    public void CompatibleConversions_UseDecimalArithmetic(double pack, IngredientUnit packUnit, double used, IngredientUnit usedUnit, double expected)
    {
        var inputs = new ProductCostInputs([new("Flour", (decimal)pack, packUnit, 10000, (decimal)used, usedUnit)], 1, null, null);
        var result = calculator.Calculate(inputs);
        Assert.Equal((decimal)expected, result.Result.IngredientCosts[0]); Assert.Null(result.Inputs.PackagingPerUnit);
        Assert.Null(result.Inputs.OtherVariableCostPerUnit);
    }
    [Theory]
    [InlineData(IngredientUnit.Gram, IngredientUnit.Millilitre)]
    [InlineData(IngredientUnit.Kilogram, IngredientUnit.Litre)]
    [InlineData(IngredientUnit.Item, IngredientUnit.Gram)]
    [InlineData((IngredientUnit)999, IngredientUnit.Gram)]
    public void IncompatibleAndUnknownUnits_AreRejected(IngredientUnit pack, IngredientUnit used) =>
        Assert.Throws<ProductCostValidationException>(() => calculator.Calculate(new([new("Flour", 1, pack, 10, 1, used)], 1, 0, 0)));

    [Theory]
    [InlineData("pack", 0)] [InlineData("pack", -1)] [InlineData("pack", 1000001)] [InlineData("pack", 0.0000001)]
    [InlineData("used", 0)] [InlineData("used", -1)] [InlineData("used", 1000001)]
    [InlineData("output", 0)] [InlineData("output", -1)] [InlineData("output", 1000001)]
    [InlineData("price", -1)] [InlineData("price", 0.001)] [InlineData("price", 1000000000000)]
    [InlineData("packaging", -1)] [InlineData("other", -1)]
    public void InvalidValuesAndBounds_AreRejected(string field, double value)
    {
        var input = Batch(); var line = input.Ingredients[0]; var amount = (decimal)value;
        input = field switch {
            "pack" => input with { Ingredients = [line with { PackQuantity = amount }] },
            "used" => input with { Ingredients = [line with { UsedQuantity = amount }] },
            "price" => input with { Ingredients = [line with { PackPrice = amount }] },
            "output" => input with { UsableOutput = amount },
            "packaging" => input with { PackagingPerUnit = amount }, _ => input with { OtherVariableCostPerUnit = amount }
        };
        Assert.Throws<ProductCostValidationException>(() => calculator.Calculate(input));
    }
    [Fact]
    public void TinyDenominatorOrOversizedResult_FailsSafely_WithoutOverflowEscaping()
    {
        var input = new ProductCostInputs([new("Flour", 0.000001m, IngredientUnit.Gram,
            ProductCostCalculator.MaximumInputCost, 1000000, IngredientUnit.Kilogram)], 0.000001m, null, null);
        Assert.Throws<ProductCostValidationException>(() => calculator.Calculate(input));
        Assert.Throws<ProductCostValidationException>(() => calculator.Calculate(Batch() with { UsableOutput = decimal.MaxValue }));
    }
    [Fact]
    public void OnlyFinalUnitCostIsRounded_AwayFromZero()
    {
        var input = new ProductCostInputs([new("A", 1, IngredientUnit.Item, 0.01m, 0.5m, IngredientUnit.Item),
            new("B", 1, IngredientUnit.Item, 0.01m, 0.5m, IngredientUnit.Item)], 1, null, null);
        var result = calculator.Calculate(input);
        Assert.Equal(0.005m, result.Result.IngredientCosts[0]); Assert.Equal(0.01m, result.Result.EstimatedUnitCost);
        Assert.Equal(0.01m, calculator.Calculate(input with { Ingredients = [input.Ingredients[0]] }).Result.EstimatedUnitCost);
    }
    [Fact]
    public void InvalidNamesAndIngredientCounts_AreRejected()
    {
        Assert.Throws<ProductCostValidationException>(() => calculator.Calculate(Batch() with { Ingredients = [] }));
        Assert.Throws<ProductCostValidationException>(() => calculator.Calculate(Batch() with { Ingredients = Enumerable.Repeat(Batch().Ingredients[0], 31).ToArray() }));
        Assert.Throws<ProductCostValidationException>(() => calculator.Calculate(Batch() with { Ingredients = [Batch().Ingredients[0] with { Name = " " }] }));
        Assert.Throws<ProductCostValidationException>(() => calculator.Calculate(Batch() with { Ingredients = [Batch().Ingredients[0] with { Name = new string('a', 101) }] }));
    }
    [Fact]
    public void Product_SetsConsistentCostAndBreakdown_AndManualChangesClearIt()
    {
        var product = new Product(Guid.NewGuid(), Guid.NewGuid(), "Coffee", "Drinks", null, 2000, 500);
        Assert.Null(product.CostBreakdown);
        var mutable = Batch().Ingredients.ToList(); var inputs = Batch() with { Ingredients = mutable };
        product.ApplyCostBreakdown(inputs); mutable.Clear();
        Assert.Equal(1400, product.UnitCost); Assert.Single(product.CostBreakdown!.Inputs.Ingredients);
        product.UpdateFinancials(3000, 1400); Assert.NotNull(product.CostBreakdown);
        product.UpdateFinancials(3000, 1500); Assert.Null(product.CostBreakdown);
        Assert.Equal(1500, product.UnitCost);
    }
}
