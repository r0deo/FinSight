using System.Globalization;
using FinSight.Domain.Enums;
using FinSight.Domain.Services;

namespace FinSight.UnitTests.Domain.Services;

public sealed class PricingDecisionCalculatorTests
{
    private readonly PricingDecisionCalculator calculator = new(new PricingSimulationCalculator());

    [Fact]
    public void Threshold_RoundsUp_AndScenariosUseThatQuantity()
    {
        var result = calculator.Calculate(4000, 5000, 1000, 33, 25, 10);

        Assert.Equal(99000, result.CurrentContribution);
        Assert.Equal(25, result.MinimumSalesToMaintainContribution);
        Assert.Equal(-8, result.QuantityChange);
        Assert.Equal(-8m * 100 / 33, result.QuantityChangePercent);
        Assert.Equal(100000, result.ExpectedScenario!.Contribution);
        Assert.Equal(125000, result.ExpectedScenario.Revenue);
        Assert.Equal(25000, result.ExpectedScenario.TotalUnitCosts);
        Assert.Equal(96000, calculator.Calculate(4000, 5000, 1000, 33, 24, 10)
            .ExpectedScenario!.Contribution);
        Assert.Equal(29, result.Scenarios.Single(x => x.Kind == PricingScenarioKind.LowerSales).Quantity);
    }

    [Fact]
    public void LowerPrice_RequiresMoreSales_AndEqualPriceWithEqualQuantityIsUnchanged()
    {
        Assert.Equal(38, calculator.Calculate(5000, 4500, 1000, 33, null, 10)
            .MinimumSalesToMaintainContribution);
        var unchanged = calculator.Calculate(4000, 4000, 1000, 33, 33, 10);
        Assert.Equal(0, unchanged.ExpectedScenario!.Difference);
    }

    [Fact]
    public void NonpositiveUnitContribution_CannotMaintainPositiveBaseline()
    {
        var atCost = calculator.Calculate(4000, 1000, 1000, 33, null, 10);
        Assert.Equal(MaintenanceThresholdStatus.ImpossibleAtPrice, atCost.ThresholdStatus);
        Assert.Null(atCost.MinimumSalesToMaintainContribution);
        var belowCost = calculator.Calculate(4000, 500, 1000, 33, null, 10);
        Assert.True(belowCost.IsProposedPriceBelowCost);
        Assert.Null(belowCost.ExpectedScenario);
    }

    [Fact]
    public void NonpositiveBaseline_HasNoStandardThreshold()
    {
        Assert.Equal(MaintenanceThresholdStatus.NoPositiveBaseline,
            calculator.Calculate(4000, 5000, 1000, 0, null, 10).ThresholdStatus);
        Assert.Equal(MaintenanceThresholdStatus.NoPositiveBaseline,
            calculator.Calculate(500, 5000, 1000, 33, null, 10).ThresholdStatus);
    }

    [Fact]
    public void UnknownExpected_StaysUnknown_AndDeclineRoundsDown()
    {
        var result = calculator.Calculate(4000, 5000, 1000, 33, null, 10);
        Assert.Null(result.ExpectedScenario);
        Assert.Equal(29, result.Scenarios.Single(x => x.Kind == PricingScenarioKind.LowerSales).Quantity);
        Assert.Equal(24, calculator.Calculate(4000, 5000, 1000, 33, null, 25)
            .Scenarios.Single(x => x.Kind == PricingScenarioKind.LowerSales).Quantity);
    }

    [Fact]
    public void InvalidInputs_AndUnsupportedThreshold_AreHandled()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(1, -1, 0, 1, null, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(1, 1, 0, 1, -1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(1, 1, 0, 1, null, 101));
        var huge = calculator.Calculate(999999999999999999m, 0.01m, 0, int.MaxValue, null, null);
        Assert.Equal(MaintenanceThresholdStatus.OutsideSupportedQuantity, huge.ThresholdStatus);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar-IQ")]
    [InlineData("ku-Arab-IQ")]
    public void Calculation_IsIdenticalAcrossWebsiteCultures(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var result = calculator.Calculate(4000, 5000, 1000, 33, 35, 10);
            Assert.Equal(25, result.MinimumSalesToMaintainContribution);
            Assert.Equal(99000, result.CurrentContribution);
            Assert.Equal(41000, result.ExpectedScenario!.Difference);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
