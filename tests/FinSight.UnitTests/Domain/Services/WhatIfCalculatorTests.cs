using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using FinSight.Domain.Services;

namespace FinSight.UnitTests.Domain.Services;

public class WhatIfCalculatorTests
{
    private readonly WhatIfCalculator _calculator = new(new PricingSimulationCalculator());

    private static WhatIfInputs Inputs(decimal price = 4000, decimal cost = 1000,
        int quantity = 100, decimal increase = 20, decimal decline = 10,
        decimal? alternativePrice = null, int? priceQuantity = null,
        decimal? supplierCost = null, int? supplierQuantity = null) =>
        new(price, cost, quantity, CostShockInputMode.Percentage, increase, null,
            decline, alternativePrice, priceQuantity, supplierCost, supplierQuantity);

    [Fact]
    public void ReferenceCase_ComparesAllThreeResponsesAndThreshold()
    {
        var result = _calculator.Calculate(Inputs(alternativePrice: 4500,
            supplierCost: 1100));

        Assert.Equal(300000, result.BaselineContribution);
        Assert.Equal(1200, result.ShockedUnitCost);
        Assert.Equal(90, result.ShockedQuantity);
        Assert.Equal(252000, result.NoActionContribution);
        Assert.Equal(-48000, result.DifferenceFromBaseline);
        Assert.Equal(3, result.Options.Count);
        var price = result.Options.Single(x => x.Kind == WhatIfOptionKind.ChangeSellingPrice);
        Assert.Equal(297000, price.Contribution);
        Assert.Equal(45000, price.DifferenceFromNoAction);
        Assert.Equal(-3000, price.DifferenceFromBaseline);
        Assert.Equal(91, price.MinimumUnitsToMaintainBaseline);
        var supplier = result.Options.Single(x => x.Kind == WhatIfOptionKind.AlternativeSupplier);
        Assert.Equal(1100, supplier.UnitCost);
        Assert.Equal(261000, supplier.Contribution);
        Assert.Equal(9000, supplier.DifferenceFromNoAction);
    }

    [Theory]
    [InlineData(20, 0, 280000)]
    [InlineData(0, 10, 270000)]
    [InlineData(0, 0, 300000)]
    [InlineData(20, 100, 0)]
    public void IndependentShockOracles(decimal increase, decimal decline, decimal expected)
    {
        Assert.Equal(expected, _calculator.Calculate(Inputs(increase: increase, decline: decline)).NoActionContribution);
    }

    [Theory]
    [InlineData(1000, 0, -18000)]
    [InlineData(500, -50000, -63000)]
    public void NonpositiveBaseline_HasNoMaintenanceThreshold(decimal price, decimal baseline, decimal shocked)
    {
        var result = _calculator.Calculate(Inputs(price: price));
        Assert.Equal(baseline, result.BaselineContribution);
        Assert.Equal(shocked, result.NoActionContribution);
        Assert.Equal(MaintenanceThresholdStatus.NoPositiveBaseline, result.Options[0].ThresholdStatus);
        Assert.True(result.Options[0].IsBelowCost);
    }

    [Fact]
    public void AlternativeResponses_CanOvershootOrWorsenWithoutChangingInputs()
    {
        var input = Inputs(alternativePrice: 5000, supplierCost: 2000);
        var result = _calculator.Calculate(input);
        Assert.Equal(342000, result.Options[1].Contribution);
        Assert.Equal(42000, result.Options[1].DifferenceFromBaseline);
        Assert.Equal(180000, result.Options[2].Contribution);
        Assert.Equal(-72000, result.Options[2].DifferenceFromNoAction);
        Assert.Equal(1000, input.CurrentUnitCost);
        Assert.Throws<ArgumentException>(() => _calculator.Calculate(Inputs() with { AlternativePriceQuantity = 1 }));
    }

    [Fact]
    public void DirectCostMode_UsesEnteredTotalCostWithoutAddingIncreaseAgain()
    {
        var input = Inputs() with { CostInputMode = CostShockInputMode.NewUnitCost,
            CostIncreasePercent = null, NewUnitCost = 1100, AlternativeSupplierCost = 1050 };
        var result = _calculator.Calculate(input);
        Assert.Equal(1100, result.ShockedUnitCost);
        Assert.Equal(261000, result.NoActionContribution);
        Assert.Equal(1050, result.Options.Last().UnitCost);
    }

    [Fact]
    public void NoShockAndOptionalResponsesBlank_OnlyShowsNoAction()
    {
        var result = _calculator.Calculate(Inputs(increase: 0, decline: 0));
        Assert.Single(result.Options);
        Assert.Equal(result.BaselineContribution, result.NoActionContribution);
        Assert.Equal(0, result.DifferenceFromBaseline);
    }

    [Fact]
    public void FullDeclineAndZeroBaselineQuantity_AreExplicit()
    {
        var full = _calculator.Calculate(Inputs(decline: 100));
        Assert.Equal(0, full.ShockedQuantity);
        Assert.Equal(0, full.NoActionContribution);
        var zero = _calculator.Calculate(Inputs(quantity: 0));
        Assert.Equal(MaintenanceThresholdStatus.NoPositiveBaseline,
            zero.Options[0].ThresholdStatus);
        Assert.Null(zero.Options[0].MinimumUnitsToMaintainBaseline);
    }

    [Fact]
    public void EqualAndBelowCostPrices_HaveNoPositiveMaintenanceThreshold()
    {
        var equal = _calculator.Calculate(Inputs(alternativePrice: 1200));
        var below = _calculator.Calculate(Inputs(alternativePrice: 1000));
        Assert.Equal(MaintenanceThresholdStatus.ImpossibleAtPrice,
            equal.Options[1].ThresholdStatus);
        Assert.False(equal.Options[1].IsBelowCost);
        Assert.True(below.Options[1].IsBelowCost);
        Assert.Null(below.Options[1].MinimumUnitsToMaintainBaseline);
    }

    [Fact]
    public void FractionalDecline_FloorsUnitsAndCostIncreaseRoundsToCents()
    {
        var result = _calculator.Calculate(Inputs(cost: 10.01m, quantity: 33,
            increase: 0.5m, decline: 24.2m));
        Assert.Equal(10.06m, result.ShockedUnitCost);
        Assert.Equal(25, result.ShockedQuantity);
    }

    [Fact]
    public void InvalidModesBoundsAndPrecision_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(Inputs(decline: 100.01m)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(Inputs(price: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(Inputs(quantity: WhatIfCalculator.MaxQuantity + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(Inputs() with
            { CostInputMode = CostShockInputMode.NewUnitCost, CostIncreasePercent = null, NewUnitCost = 999 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(Inputs() with
            { AlternativePrice = 12.345m }));
        Assert.Throws<ArgumentException>(() => _calculator.Calculate(Inputs() with
            { NewUnitCost = 1500 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(Inputs(
            cost: WhatIfCalculator.MaxUnitAmount, increase: 10)));
    }

    [Fact]
    public void MaximumInputs_DoNotOverflowStoredMoneyRange()
    {
        var result = _calculator.Calculate(Inputs(price: WhatIfCalculator.MaxUnitAmount,
            cost: 0, quantity: WhatIfCalculator.MaxQuantity, increase: 0, decline: 0));
        Assert.Equal(999_999_999_990_000m, result.BaselineContribution);
    }

    [Fact]
    public void ThresholdBeyondSupportedQuantity_IsNotDisplayedAsAnAttainableNumber()
    {
        var result = _calculator.Calculate(Inputs(alternativePrice: 1200.01m));
        Assert.Equal(MaintenanceThresholdStatus.OutsideSupportedQuantity,
            result.Options[1].ThresholdStatus);
        Assert.Null(result.Options[1].MinimumUnitsToMaintainBaseline);
    }
}
