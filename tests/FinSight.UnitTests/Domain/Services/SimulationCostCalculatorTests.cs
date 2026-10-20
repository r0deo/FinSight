using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Models;
using FinSight.Domain.Services;

namespace FinSight.UnitTests.Domain.Services;

public sealed class SimulationCostCalculatorTests
{
    private static readonly DateOnly Month = new(2026, 10, 1);
    private static LinkedCostEntry Entry(BusinessCostCategory category, decimal amount,
        UnitCostInclusion inclusion = UnitCostInclusion.No, CostCertainty certainty = CostCertainty.Actual) =>
        new(Guid.NewGuid(), category.ToString(), category, amount, certainty, inclusion, 1, false);
    private static LinkedCostEntry[] Costs() => [
        Entry(BusinessCostCategory.Rent, 1000000), Entry(BusinessCostCategory.Labor, 1500000),
        Entry(BusinessCostCategory.Energy, 500000)];
    private static SimulationCostContext Calculate(CostLinkOptions settings, decimal proposed = 600000) =>
        SimulationCostCalculator.Calculate(settings, SalesPeriod.Month, Costs(), true, 500000,
            [new("Proposed", proposed)]);

    [Fact]
    public void ReferenceFixture_IndependentSwitchesAndDecisionCost_ProduceAThroughE()
    {
        var a = Calculate(new());
        Assert.False(a.Settings.Enabled); Assert.Empty(a.Entries); Assert.Null(a.AllocationAmount);
        Assert.Equal(500000, a.CurrentContribution); Assert.Equal(600000, a.Options.Single().Contribution);
        var b = Calculate(new(true, Month));
        Assert.Equal(3000000, b.RecordedBasis); Assert.Null(b.AllocationAmount);
        Assert.Equal(600000, b.Options.Single().Contribution);
        var c = Calculate(new(true, Month, true, 10, true));
        Assert.Equal(300000, c.AllocationAmount); Assert.Equal(200000, c.CurrentRemainder);
        Assert.Equal(300000, c.Options.Single().Remainder);
        Assert.Equal(100000, c.Options.Single().Remainder - c.CurrentRemainder);
        var decision = new PricingDecisionCalculator(new()).Calculate(10000, 11000, 5000, 100, 100, null, 50000);
        Assert.Equal(550000, decision.Scenarios[0].Contribution);
        Assert.Equal(50000, decision.Scenarios[0].Difference);
        Assert.Equal(92, decision.MinimumSalesToMaintainContribution);
        var d = Calculate(new(true, Month, true, 10, true), decision.Scenarios[0].Contribution);
        Assert.Equal(250000, d.Options.Single().Remainder);
        var e = Calculate(new(false, Month, true, 10, true), decision.Scenarios[0].Contribution);
        Assert.Null(e.AllocationAmount); Assert.Null(e.Settings.Percentage); Assert.False(e.Settings.Allocate);
        Assert.Equal(550000, e.Options.Single().Contribution);
    }

    [Theory]
    [InlineData(-1)] [InlineData(101)] [InlineData(0.001)]
    public void Allocation_RejectsInvalidPercentages(decimal percent) =>
        Assert.Throws<CostContextValidationException>(() => Calculate(new(true, Month, true, percent, true)));

    [Fact]
    public void Allocation_MissingIsInvalidButExplicitZeroIsValid_AndDisabledValuesAreIgnored()
    {
        Assert.Throws<CostContextValidationException>(() => Calculate(new(true, Month, true, null, true)));
        Assert.Equal(0, Calculate(new(true, Month, true, 0, true)).AllocationAmount);
        Assert.Null(Calculate(new(false, null, true, -99, true)).AllocationAmount);
        Assert.Null(Calculate(new(true, Month, false, -99, true)).Settings.Percentage);
        Assert.Throws<CostContextValidationException>(() => Calculate(new(true)));
    }

    [Fact]
    public void Classification_ExcludesInventoryAmbiguousAndAlreadyIncludedCosts_WithoutInventingRecurrence()
    {
        var costs = Costs().Concat(new[] {
            Entry(BusinessCostCategory.Inventory, 800000), Entry(BusinessCostCategory.Other, 200000),
            Entry(BusinessCostCategory.Maintenance, 300000), Entry(BusinessCostCategory.Transport, 100000),
            Entry(BusinessCostCategory.Labor, 500000, UnitCostInclusion.Yes),
            Entry(BusinessCostCategory.Energy, 500000, UnitCostInclusion.Unsure, CostCertainty.Estimated) }).ToArray();
        var result = SimulationCostCalculator.Calculate(new(true, Month, true, 10, false),
            SalesPeriod.Month, costs, false, 500000, [new("Proposed", 600000)]);
        Assert.Equal(3000000, result.RecordedBasis); Assert.Equal(3, result.Entries.Count(x => x.Eligible));
        Assert.True(result.HasEstimates); Assert.False(result.MonthReviewed);
        Assert.Null(result.AllocationAmount); Assert.Equal("UnconfirmedBasis", result.Limitation);
    }

    [Theory]
    [InlineData(SalesPeriod.Day)] [InlineData(SalesPeriod.Week)]
    public void UnsupportedPeriods_AreContextOnly(SalesPeriod period)
    {
        var result = SimulationCostCalculator.Calculate(new(true, Month, true, 10, true),
            period, Costs(), true, 500000, [new("Proposed", 600000)]);
        Assert.Equal("PeriodMismatch", result.Limitation); Assert.Null(result.AllocationAmount);
    }

    [Fact]
    public void MissingCostsAreUnknown_ExplicitZeroIsZero_IncompleteDeliveryCannotAllocatePerOrder()
    {
        var settings = new CostLinkOptions(true, Month, true, 10, true);
        var missing = SimulationCostCalculator.Calculate(settings, SalesPeriod.Month, [], false, 1, []);
        Assert.Null(missing.RecordedBasis); Assert.Equal("NoRecords", missing.Limitation);
        var zero = SimulationCostCalculator.Calculate(settings, SalesPeriod.Month,
            [Entry(BusinessCostCategory.Rent, 0)], false, 1, []);
        Assert.Equal(0, zero.RecordedBasis); Assert.Equal(0, zero.AllocationAmount);
        var perOrder = SimulationCostCalculator.Calculate(settings, SalesPeriod.Month, Costs(), true, null, []);
        Assert.Equal("NoPeriodComparison", perOrder.Limitation); Assert.Null(perOrder.AllocationAmount);
    }

    [Fact]
    public void Allocation_RoundsMoney_AndRejectsOverflow()
    {
        var result = SimulationCostCalculator.Calculate(new(true, Month, true, 50, true),
            SalesPeriod.Month, [Entry(BusinessCostCategory.Rent, 0.01m)], true, 0, [new("Proposed", 0)]);
        Assert.Equal(0.01m, result.AllocationAmount); Assert.Equal(-0.01m, result.CurrentRemainder);
        Assert.Throws<CostContextValidationException>(() => SimulationCostCalculator.Calculate(new(true, Month),
            SalesPeriod.Month, [Entry(BusinessCostCategory.Rent, decimal.MaxValue),
                Entry(BusinessCostCategory.Rent, decimal.MaxValue)], true, 0, []));
    }

    [Theory]
    [InlineData(-1)] [InlineData(0.001)]
    public void AdditionalDecisionCost_RejectsInvalidAmounts(decimal cost) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PricingDecisionCalculator(new()).Calculate(10, 12, 5, 100, 100, null, cost));

    [Fact]
    public void AdditionalDecisionCost_BlankAndZeroRemainDistinct()
    {
        var calculator = new PricingDecisionCalculator(new());
        Assert.Null(calculator.Calculate(10,12,5,100,100,null).AdditionalDecisionCost);
        Assert.Equal(0, calculator.Calculate(10,12,5,100,100,null,0).AdditionalDecisionCost);
    }
}
