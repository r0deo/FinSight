using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Models;
using FinSight.Domain.Services;

namespace FinSight.UnitTests.Domain.Services;

public sealed class DeliverySimulationCalculatorTests
{
    private readonly DeliverySimulationCalculator calculator = new(new());
    public static DeliveryAssumptions Inputs => new(1, 6000, 25, 500, 0, 20000, SalesPeriod.Month, 100, 50, 10);

    [Theory]
    [InlineData(10, 300000, 0)]
    [InlineData(0, 330000, 30000)]
    [InlineData(50, 180000, -120000)]
    public void Reference_SeparatesTransferredFromNewOrders(int transferred, int scenario, int difference)
    {
        var result = calculator.Calculate(6000, 3000, Inputs with { TransferredOrders = transferred }).Result;
        Assert.Equal(3000, result.DirectContributionPerOrder); Assert.Equal(1500, result.CommissionPerOrder);
        Assert.Equal(6000, result.ProductSubtotal); Assert.Equal(3000, result.RecordedCostPerOrder);
        Assert.Equal(1000, result.DeliveryContributionPerOrder); Assert.Equal(300000, result.BaselineContribution);
        Assert.Equal(100 - transferred, result.RemainingDirectOrders); Assert.Equal(50 - transferred, result.NewDeliveryOrders);
        Assert.Equal(scenario, result.ScenarioContribution); Assert.Equal(difference, result.Difference);
    }

    [Fact]
    public void MultipleUnits_ApplyPackagingAndOtherChargesOncePerOrder()
    {
        var result = calculator.Calculate(6000, 3000, Inputs with { UnitsPerOrder = 2, OtherChargesPerOrder = 100 }).Result;
        Assert.Equal(6000, result.DirectContributionPerOrder); Assert.Equal(12000, result.ProductSubtotal);
        Assert.Equal(3000, result.CommissionPerOrder); Assert.Equal(6000, result.RecordedCostPerOrder);
        Assert.Equal(2400, result.DeliveryContributionPerOrder); Assert.Equal(640000, result.ScenarioContribution);
    }

    [Theory]
    [InlineData(0, 2500)]
    [InlineData(100, -3500)]
    public void CommissionEndpoints_AndNegativeContribution_AreSupported(int commission, int contribution)
    {
        var result = calculator.Calculate(6000, 3000, Inputs with { CommissionPercentage = commission }).Result;
        Assert.Equal(contribution, result.DeliveryContributionPerOrder);
    }

    [Fact]
    public void ZeroContribution_AndFeeRounding_AreConsistentWithPeriodTotals()
    {
        Assert.Equal(0, calculator.Calculate(6000, 3000, Inputs with { PackagingPerOrder = 1500 }).Result.DeliveryContributionPerOrder);
        var result = calculator.Calculate(0.05m, 0.01m, Inputs with { DeliveryUnitPrice = 0.05m, CommissionPercentage = 50,
            PackagingPerOrder = 0, FixedPeriodCost = 0, CurrentDirectOrders = 0, ExpectedDeliveryOrders = 3, TransferredOrders = 0 }).Result;
        Assert.Equal(0.03m, result.CommissionPerOrder); Assert.Equal(0.01m, result.DeliveryContributionPerOrder);
        Assert.Equal(0.03m, result.ScenarioContribution);
    }

    [Fact]
    public void MissingComparisonCounts_DoNotBecomeZero_AndFixedCostUnknownIsPreserved()
    {
        foreach (var input in new[] { Inputs with { CurrentDirectOrders = null }, Inputs with { ExpectedDeliveryOrders = null }, Inputs with { TransferredOrders = null } })
        {
            var result = calculator.Calculate(6000, 3000, input).Result;
            Assert.Equal(1000, result.DeliveryContributionPerOrder); Assert.Null(result.BaselineContribution);
            Assert.Null(result.ScenarioContribution); Assert.Null(result.Difference); Assert.Null(result.NewDeliveryOrders);
        }
        var unknown = calculator.Calculate(6000, 3000, Inputs with { FixedPeriodCost = null });
        var zero = calculator.Calculate(6000, 3000, Inputs with { FixedPeriodCost = 0 });
        Assert.Equal(unknown.Result, zero.Result); Assert.Null(unknown.Inputs.FixedPeriodCost); Assert.Equal(0, zero.Inputs.FixedPeriodCost);
        var noOrders = calculator.Calculate(6000, 3000, Inputs with { CurrentDirectOrders = 0, ExpectedDeliveryOrders = 0, TransferredOrders = 0 });
        Assert.Equal(-20000, noOrders.Result.ScenarioContribution);
    }

    public static IEnumerable<object[]> InvalidInputs()
    {
        yield return [Inputs with { UnitsPerOrder = 0 }];
        yield return [Inputs with { UnitsPerOrder = -1 }];
        yield return [Inputs with { DeliveryUnitPrice = 0 }];
        yield return [Inputs with { DeliveryUnitPrice = -1 }];
        yield return [Inputs with { DeliveryUnitPrice = 0.001m }];
        yield return [Inputs with { DeliveryUnitPrice = decimal.MaxValue }];
        yield return [Inputs with { CommissionPercentage = -1 }];
        yield return [Inputs with { CommissionPercentage = 101 }];
        yield return [Inputs with { CommissionPercentage = 1.00001m }];
        yield return [Inputs with { PackagingPerOrder = -1 }];
        yield return [Inputs with { OtherChargesPerOrder = -1 }];
        yield return [Inputs with { FixedPeriodCost = -1 }];
        yield return [Inputs with { CurrentDirectOrders = -1 }];
        yield return [Inputs with { ExpectedDeliveryOrders = -1 }];
        yield return [Inputs with { TransferredOrders = -1 }];
        yield return [Inputs with { TransferredOrders = 51 }];
        yield return [Inputs with { CurrentDirectOrders = 9 }];
        yield return [Inputs with { ExpectedDeliveryOrders = null, CurrentDirectOrders = 9 }];
        yield return [Inputs with { CurrentDirectOrders = null, ExpectedDeliveryOrders = 9 }];
        yield return [Inputs with { ComparisonPeriod = (SalesPeriod)999 }];
        yield return [Inputs with { DeliveryUnitPrice = DeliverySimulationCalculator.MaximumMoney, UnitsPerOrder = int.MaxValue }];
        yield return [Inputs with { DeliveryUnitPrice = DeliverySimulationCalculator.MaximumMoney / 10 + 0.001m, ExpectedDeliveryOrders = int.MaxValue }];
    }
    [Theory, MemberData(nameof(InvalidInputs))]
    public void InvalidValues_ThrowReadableFieldOrRangeErrors(DeliveryAssumptions input)
    {
        Assert.False(string.IsNullOrWhiteSpace(Assert.Throws<DeliveryValidationException>(() => calculator.Calculate(6000, 3000, input)).Message));
    }

    [Fact]
    public void SavedDelivery_EditUsesOriginalPrices_AndLifecycleRetainsSnapshot()
    {
        var now = DateTime.UtcNow;
        var simulation = PricingSimulation.CreateDelivery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            6000, 3000, Inputs, "IQD", "Coffee", now, calculator);
        simulation.UpdateDelivery(Inputs with { TransferredOrders = 0 }, now.AddSeconds(1), 1, calculator);
        Assert.Equal(6000, simulation.CurrentPrice); Assert.Equal(3000, simulation.UnitCost);
        Assert.Equal(330000, simulation.DeliverySnapshot!.Result.ScenarioContribution); Assert.Equal(2, simulation.Version);
        var snapshot = simulation.DeliverySnapshot;
        Assert.Throws<PricingSimulationConflictException>(() => simulation.UpdateDelivery(Inputs, now, 1, calculator));
        Assert.Throws<PricingSimulationConflictException>(() => simulation.Update(5000, 100, 150, now, new(), 2));
        Assert.Throws<PricingSimulationConflictException>(() => simulation.UpdateDiscount(10, 100, 150, 0, SalesPeriod.Week, now, 2, new(new())));
        simulation.Archive(now.AddSeconds(2), 2);
        Assert.Throws<PricingSimulationConflictException>(() => simulation.UpdateDelivery(Inputs, now, 3, calculator));
        simulation.Unarchive(now.AddSeconds(3), 3); simulation.Delete(now.AddSeconds(4), 4);
        Assert.Throws<PricingSimulationConflictException>(() => simulation.UpdateDelivery(Inputs, now, 5, calculator));
        simulation.Restore(now.AddSeconds(5), 5);
        Assert.Equal(snapshot, simulation.DeliverySnapshot); Assert.Equal(6, simulation.Version);
    }
}
