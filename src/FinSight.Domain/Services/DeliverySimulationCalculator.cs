using FinSight.Domain.Exceptions;
using FinSight.Domain.Models;

namespace FinSight.Domain.Services;

public sealed class DeliverySimulationCalculator(PricingSimulationCalculator calculator)
{
    public const decimal MaximumMoney = 9_999_999_999_999_999.99m;

    public static void ValidateAssumptions(DeliveryAssumptions input)
    {
        if (input is null) throw new DeliveryValidationException("", "Enter the delivery assumptions.");
        if (input.UnitsPerOrder <= 0) throw new DeliveryValidationException(nameof(input.UnitsPerOrder), "Enter a positive whole number of units per order.");
        Money(input.DeliveryUnitPrice, nameof(input.DeliveryUnitPrice));
        if (input.DeliveryUnitPrice == 0) throw new DeliveryValidationException(nameof(input.DeliveryUnitPrice), "Enter a delivery unit price greater than zero.");
        if (input.CommissionPercentage < 0 || input.CommissionPercentage > 100 || decimal.Round(input.CommissionPercentage, 4) != input.CommissionPercentage)
            throw new DeliveryValidationException(nameof(input.CommissionPercentage), "Enter commission from 0 to 100%, with at most four decimal places.");
        Money(input.PackagingPerOrder, nameof(input.PackagingPerOrder));
        Money(input.OtherChargesPerOrder, nameof(input.OtherChargesPerOrder));
        if (input.FixedPeriodCost is { } fixedCost) Money(fixedCost, nameof(input.FixedPeriodCost));
        if (!Enum.IsDefined(input.ComparisonPeriod)) throw new DeliveryValidationException(nameof(input.ComparisonPeriod), "Choose a valid comparison period.");
        if (input.CurrentDirectOrders < 0) CountError(nameof(input.CurrentDirectOrders));
        if (input.ExpectedDeliveryOrders < 0) CountError(nameof(input.ExpectedDeliveryOrders));
        if (input.TransferredOrders < 0) CountError(nameof(input.TransferredOrders));
        if (input.TransferredOrders > input.CurrentDirectOrders || input.TransferredOrders > input.ExpectedDeliveryOrders)
            throw new DeliveryValidationException(nameof(input.TransferredOrders), "Transferred orders cannot exceed current direct orders or expected delivery orders.");
    }

    public DeliverySimulationSnapshot Calculate(decimal currentPrice, decimal unitCost, DeliveryAssumptions input)
    {
        ValidateAssumptions(input); Money(currentPrice, nameof(currentPrice)); Money(unitCost, nameof(unitCost));
        try
        {
            var direct = calculator.Calculate(currentPrice, input.DeliveryUnitPrice, unitCost, input.UnitsPerOrder, input.UnitsPerOrder).CurrentContribution;
            var subtotal = input.UnitsPerOrder * input.DeliveryUnitPrice;
            // Money uses two decimals, midpoint away from zero. Round the fee per order once,
            // then multiply that same displayed/stored fee for all modeled orders.
            var commission = decimal.Round(subtotal * input.CommissionPercentage / 100, 2, MidpointRounding.AwayFromZero);
            var recordedCost = input.UnitsPerOrder * unitCost;
            var delivery = subtotal - commission - recordedCost - input.PackagingPerOrder - input.OtherChargesPerOrder;
            foreach (var amount in new[] { direct, subtotal, commission, recordedCost, delivery }) Bound(amount);
            decimal? baseline = null, scenario = null, difference = null;
            int? remaining = null, newOrders = null;
            if (input.CurrentDirectOrders is { } q && input.ExpectedDeliveryOrders is { } n && input.TransferredOrders is { } m)
            {
                remaining = q - m; newOrders = n - m;
                baseline = q * direct;
                scenario = remaining.Value * direct + n * delivery - (input.FixedPeriodCost ?? 0);
                difference = scenario - baseline;
                Bound(baseline.Value); Bound(scenario.Value); Bound(difference.Value);
            }
            return new(currentPrice, unitCost, input, new(direct, subtotal, commission, recordedCost, delivery,
                baseline, scenario, difference, remaining, newOrders));
        }
        catch (OverflowException)
        {
            throw new DeliveryValidationException("", "These prices, order counts or charges exceed the supported calculation range.");
        }
    }

    private static void Bound(decimal value) { if (Math.Abs(value) > MaximumMoney) throw new OverflowException(); }
    private static void CountError(string field) => throw new DeliveryValidationException(field, "Enter a non-negative whole number of orders.");
    private static void Money(decimal value, string field)
    {
        if (value < 0 || value > MaximumMoney || decimal.Round(value, 2) != value)
            throw new DeliveryValidationException(field, "Enter a non-negative amount with at most two decimal places within the supported range.");
    }
}
