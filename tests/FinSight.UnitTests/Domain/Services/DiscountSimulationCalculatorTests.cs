using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Models;
using FinSight.Domain.Services;

namespace FinSight.UnitTests.Domain.Services;

public sealed class DiscountSimulationCalculatorTests
{
    private readonly DiscountSimulationCalculator calculator = new(new PricingSimulationCalculator());
    private static DiscountInputs Inputs => new(5000, 3000, 10, 100, 140, 0);

    [Theory]
    [InlineData(120, 0, 134, 180000, -20000)]
    [InlineData(140, 0, 134, 210000, 10000)]
    [InlineData(140, 30000, 154, 180000, -20000)]
    public void ReferenceFixture_UsesCampaignCostAndCeiling(int expected, int campaign, int minimum, int total, int difference)
    {
        var result = calculator.Calculate(Inputs with { ExpectedSalesQuantity = expected, CampaignCost = campaign }).Result;
        Assert.Equal(4500, result.DiscountedPrice); Assert.Equal(200000, result.CurrentContribution);
        Assert.Equal(1500, result.ContributionPerUnit); Assert.Equal(minimum, result.MinimumUnits);
        Assert.Equal(minimum - 100, result.AdditionalUnits); Assert.Equal(minimum - 100, result.RequiredIncreasePercentage);
        Assert.Equal(total, result.ScenarioContribution); Assert.Equal(difference, result.Difference);
    }

    [Fact]
    public void MissingInputs_AreNotFabricated_AndExplicitZeroIsPreserved()
    {
        var unknown = calculator.Calculate(Inputs with { ExpectedSalesQuantity = null, CampaignCost = null });
        Assert.Null(unknown.Inputs.CampaignCost); Assert.Null(unknown.Result.ScenarioContribution);
        Assert.Null(unknown.Result.Difference); Assert.Equal(134, unknown.Result.MinimumUnits);
        var zero = calculator.Calculate(Inputs with { ExpectedSalesQuantity = 0 });
        Assert.Equal(0, zero.Inputs.CampaignCost); Assert.Equal(0, zero.Result.ScenarioContribution);
        Assert.Equal(-200000, zero.Result.Difference);
    }

    [Theory]
    [InlineData(40, 3000, false, MaintenanceThresholdStatus.ImpossibleAtPrice)]
    [InlineData(50, 3000, true, MaintenanceThresholdStatus.ImpossibleAtPrice)]
    [InlineData(10, 5000, true, MaintenanceThresholdStatus.NoPositiveBaseline)]
    [InlineData(10, 6000, true, MaintenanceThresholdStatus.NoPositiveBaseline)]
    public void MarginsAndBaseline_SuppressMisleadingThresholds(int percentage, int cost, bool warning, MaintenanceThresholdStatus status)
    {
        var result = calculator.Calculate(Inputs with { DiscountPercentage = percentage, UnitCost = cost, CampaignCost = 30000 }).Result;
        Assert.Equal(warning, result.IsBelowCost); Assert.Equal(status, result.ThresholdStatus);
        Assert.Null(result.MinimumUnits); Assert.Null(result.AdditionalUnits); Assert.Null(result.RequiredIncreasePercentage);
    }

    [Fact]
    public void Rounding_IsAwayFromZero_AndWarningsUseActualDiscountedPrice()
    {
        var equal = calculator.Calculate(new(1.01m, 0.91m, 10, 1, 1, 0)).Result;
        Assert.Equal(0.91m, equal.DiscountedPrice); Assert.False(equal.IsBelowCost);
        Assert.Equal(0, equal.ContributionPerUnit); Assert.Null(equal.MinimumUnits);
        var midpoint = calculator.Calculate(new(0.05m, 0.02m, 50, 1, 1, 0)).Result;
        Assert.Equal(0.03m, midpoint.DiscountedPrice); Assert.Equal(3, midpoint.MinimumUnits);
    }

    public static IEnumerable<object[]> InvalidInputs()
    {
        yield return [Inputs with { CurrentPrice = -1 }];
        yield return [Inputs with { UnitCost = -1 }];
        yield return [Inputs with { CurrentPrice = 1.001m }];
        yield return [Inputs with { CurrentPrice = decimal.MaxValue }];
        yield return [Inputs with { DiscountPercentage = 0 }];
        yield return [Inputs with { DiscountPercentage = 100 }];
        yield return [Inputs with { DiscountPercentage = 0.00001m }];
        yield return [Inputs with { CurrentSalesQuantity = 0 }];
        yield return [Inputs with { ExpectedSalesQuantity = -1 }];
        yield return [Inputs with { CampaignCost = -1 }];
        yield return [Inputs with { CampaignCost = 0.001m }];
        yield return [Inputs with { CurrentPrice = DiscountSimulationCalculator.MaximumMoney, CurrentSalesQuantity = int.MaxValue }];
    }
    [Theory, MemberData(nameof(InvalidInputs))]
    public void InvalidAndOutOfRangeInputs_AreRejected(DiscountInputs inputs) =>
        Assert.Throws<DiscountValidationException>(() => calculator.Calculate(inputs));

    [Fact]
    public void EnormousThreshold_IsReportedWithoutIntegerOverflow()
    {
        var result = calculator.Calculate(new(1, 0.01m, 98, 1, null, DiscountSimulationCalculator.MaximumMoney)).Result;
        Assert.Equal(MaintenanceThresholdStatus.OutsideSupportedQuantity, result.ThresholdStatus);
        Assert.Null(result.MinimumUnits);
    }

    [Fact]
    public void SavedDiscount_EditAndLifecyclePreserveProductSnapshotAndVersion()
    {
        var now = new DateTime(2026, 10, 18, 0, 0, 0, DateTimeKind.Utc);
        var simulation = PricingSimulation.CreateDiscount(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Inputs, SalesPeriod.Week, "IQD", "Coffee", now, calculator);
        simulation.UpdateDiscount(10, 100, 120, null, SalesPeriod.Month, now.AddMinutes(1), 1, calculator);
        Assert.Equal(5000, simulation.CurrentPrice); Assert.Equal(3000, simulation.UnitCost);
        Assert.Equal(-20000, simulation.Difference); Assert.Null(simulation.DiscountSnapshot!.Inputs.CampaignCost);
        Assert.Equal(SalesPeriod.Month, simulation.ComparisonPeriod); Assert.Equal(2, simulation.Version);
        var snapshot = simulation.DiscountSnapshot;
        Assert.Throws<PricingSimulationConflictException>(() => simulation.UpdateDiscount(10, 100, 140, 0, SalesPeriod.Week, now, 1, calculator));
        Assert.Throws<PricingSimulationConflictException>(() => simulation.Update(4500, 100, 140, now, new(), 2));
        simulation.Archive(now.AddMinutes(2), 2);
        Assert.Throws<PricingSimulationConflictException>(() => simulation.UpdateDiscount(10, 100, 140, 0, SalesPeriod.Week, now, 3, calculator));
        simulation.Unarchive(now.AddMinutes(3), 3); simulation.Delete(now.AddMinutes(4), 4);
        Assert.Throws<PricingSimulationConflictException>(() => simulation.UpdateDiscount(10, 100, 140, 0, SalesPeriod.Week, now, 5, calculator));
        simulation.Restore(now.AddMinutes(5), 5);
        Assert.Equal(snapshot, simulation.DiscountSnapshot); Assert.Equal(6, simulation.Version);
    }
}
