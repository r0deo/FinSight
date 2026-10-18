using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Models;

namespace FinSight.Domain.Services;

public sealed class DiscountSimulationCalculator(PricingSimulationCalculator calculator)
{
    public const decimal MaximumMoney = 9_999_999_999_999_999.99m; // numeric(18,2)
    public DiscountSimulationSnapshot Calculate(DiscountInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Money(input.CurrentPrice, nameof(input.CurrentPrice));
        Money(input.UnitCost, nameof(input.UnitCost));
        if (input.CampaignCost is { } cost) Money(cost, nameof(input.CampaignCost));
        if (input.DiscountPercentage <= 0 || input.DiscountPercentage >= 100 || decimal.Round(input.DiscountPercentage, 4) != input.DiscountPercentage)
            throw new DiscountValidationException(nameof(input.DiscountPercentage), "Enter a discount greater than 0 and less than 100%, with at most four decimal places.");
        if (input.CurrentSalesQuantity <= 0)
            throw new DiscountValidationException(nameof(input.CurrentSalesQuantity), "Enter a positive whole number of current sales.");
        if (input.ExpectedSalesQuantity < 0)
            throw new DiscountValidationException(nameof(input.ExpectedSalesQuantity), "Expected sales must be a non-negative whole number.");
        try
        {
            // Round the selling price first; warnings and every calculation use this same actual price.
            var discounted = decimal.Round(input.CurrentPrice * (1 - input.DiscountPercentage / 100), 2, MidpointRounding.AwayFromZero);
            var baseline = calculator.Calculate(input.CurrentPrice, discounted, input.UnitCost,
                input.CurrentSalesQuantity, input.ExpectedSalesQuantity ?? 0);
            var unit = discounted - input.UnitCost;
            var campaign = input.CampaignCost ?? 0;
            decimal? scenario = input.ExpectedSalesQuantity.HasValue ? baseline.ProposedContribution - campaign : null;
            decimal? difference = scenario - baseline.CurrentContribution;
            if (Math.Abs(baseline.CurrentContribution) > MaximumMoney ||
                (scenario.HasValue && Math.Abs(scenario.Value) > MaximumMoney) ||
                (difference.HasValue && Math.Abs(difference.Value) > MaximumMoney)) throw new OverflowException();
            var threshold = baseline.CurrentContribution <= 0
                ? (MaintenanceThresholdStatus.NoPositiveBaseline, (int?)null)
                : PricingDecisionCalculator.MaintenanceThreshold(baseline.CurrentContribution + campaign, unit);
            var minimum = threshold.Item2;
            return new(input, new(discounted, baseline.CurrentContribution, unit, scenario, difference,
                threshold.Item1, minimum, minimum - input.CurrentSalesQuantity,
                minimum.HasValue ? (decimal)(minimum.Value - input.CurrentSalesQuantity) * 100 / input.CurrentSalesQuantity : null,
                discounted < input.UnitCost));
        }
        catch (OverflowException)
        {
            throw new DiscountValidationException(nameof(input.CurrentSalesQuantity), "These prices, quantities or campaign costs exceed the supported calculation range.");
        }
    }

    private static void Money(decimal amount, string field)
    {
        if (amount < 0 || amount > MaximumMoney || decimal.Round(amount, 2) != amount)
            throw new DiscountValidationException(field, "Enter a non-negative amount with at most two decimal places within the supported range.");
    }
}
