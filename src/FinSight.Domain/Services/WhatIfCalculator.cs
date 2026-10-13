using FinSight.Domain.Enums;
using FinSight.Domain.Models;

namespace FinSight.Domain.Services;

public sealed class WhatIfCalculator(PricingSimulationCalculator contributionCalculator)
{
    // Amounts and calculated totals fit the existing numeric(18,2) storage columns.
    public const decimal MaxUnitAmount = 999_999_999.99m;
    public const int MaxQuantity = 1_000_000;
    public const decimal MaxCostIncreasePercent = 10_000m;

    public WhatIfResult Calculate(WhatIfInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Money(input.CurrentPrice, nameof(input.CurrentPrice));
        Money(input.CurrentUnitCost, nameof(input.CurrentUnitCost));
        Quantity(input.CurrentQuantity, nameof(input.CurrentQuantity));
        Percent(input.SalesDeclinePercent, 100, nameof(input.SalesDeclinePercent));

        decimal shockedCost;
        switch (input.CostInputMode)
        {
            case CostShockInputMode.Percentage:
                if (!input.CostIncreasePercent.HasValue || input.NewUnitCost.HasValue)
                    throw new ArgumentException("Enter only a cost increase percentage.", nameof(input));
                Percent(input.CostIncreasePercent.Value, MaxCostIncreasePercent,
                    nameof(input.CostIncreasePercent));
                shockedCost = decimal.Round(input.CurrentUnitCost
                    * (1 + input.CostIncreasePercent.Value / 100), 2,
                    MidpointRounding.AwayFromZero);
                Money(shockedCost, nameof(input.CostIncreasePercent));
                break;
            case CostShockInputMode.NewUnitCost:
                if (!input.NewUnitCost.HasValue || input.CostIncreasePercent.HasValue)
                    throw new ArgumentException("Enter only a new unit cost.", nameof(input));
                Money(input.NewUnitCost.Value, nameof(input.NewUnitCost));
                if (input.NewUnitCost.Value < input.CurrentUnitCost)
                    throw new ArgumentOutOfRangeException(nameof(input.NewUnitCost),
                        "New unit cost cannot be below the current unit cost.");
                shockedCost = input.NewUnitCost.Value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(input.CostInputMode));
        }

        if (!input.AlternativePrice.HasValue && input.AlternativePriceQuantity.HasValue)
            throw new ArgumentException("A selling price is required for this quantity.", nameof(input));
        if (!input.AlternativeSupplierCost.HasValue && input.AlternativeSupplierQuantity.HasValue)
            throw new ArgumentException("An alternative supplier cost is required for this quantity.", nameof(input));
        if (input.AlternativePrice.HasValue)
            Money(input.AlternativePrice.Value, nameof(input.AlternativePrice));
        if (input.AlternativeSupplierCost.HasValue)
            Money(input.AlternativeSupplierCost.Value, nameof(input.AlternativeSupplierCost));
        if (input.AlternativePriceQuantity.HasValue)
            Quantity(input.AlternativePriceQuantity.Value, nameof(input.AlternativePriceQuantity));
        if (input.AlternativeSupplierQuantity.HasValue)
            Quantity(input.AlternativeSupplierQuantity.Value, nameof(input.AlternativeSupplierQuantity));

        var shockedQuantity = (int)decimal.Floor(input.CurrentQuantity
            * (1 - input.SalesDeclinePercent / 100));
        var baseline = Contribution(input.CurrentPrice, input.CurrentUnitCost, input.CurrentQuantity);
        var noAction = Contribution(input.CurrentPrice, shockedCost, shockedQuantity);
        var options = new List<WhatIfOptionResult>(3)
        {
            Option(WhatIfOptionKind.KeepCurrentPrice, input.CurrentPrice, shockedCost,
                shockedQuantity, baseline, noAction)
        };
        if (input.AlternativePrice.HasValue)
            options.Add(Option(WhatIfOptionKind.ChangeSellingPrice, input.AlternativePrice.Value,
                shockedCost, input.AlternativePriceQuantity ?? shockedQuantity, baseline, noAction));
        if (input.AlternativeSupplierCost.HasValue)
            options.Add(Option(WhatIfOptionKind.AlternativeSupplier, input.CurrentPrice,
                input.AlternativeSupplierCost.Value,
                input.AlternativeSupplierQuantity ?? shockedQuantity, baseline, noAction));
        return new WhatIfResult(baseline, shockedCost, shockedQuantity, noAction,
            noAction - baseline, options);
    }

    private WhatIfOptionResult Option(WhatIfOptionKind kind, decimal price, decimal cost,
        int quantity, decimal baseline, decimal noAction)
    {
        var contribution = Contribution(price, cost, quantity);
        var (status, minimum) = PricingDecisionCalculator.MaintenanceThreshold(
            baseline, price - cost, MaxQuantity);
        return new WhatIfOptionResult(kind, price, cost, quantity, price * quantity,
            cost * quantity, contribution, contribution - baseline,
            contribution - noAction, price < cost, status, minimum);
    }

    private decimal Contribution(decimal price, decimal cost, int quantity) =>
        contributionCalculator.Calculate(price, price, cost, quantity, quantity)
            .CurrentContribution;

    private static void Money(decimal value, string name)
    {
        if (value < 0 || value > MaxUnitAmount || decimal.Round(value, 2) != value)
            throw new ArgumentOutOfRangeException(name,
                "Enter an amount from 0 to 999,999,999.99 with at most two decimal places.");
    }

    private static void Quantity(int value, string name)
    {
        if (value < 0 || value > MaxQuantity)
            throw new ArgumentOutOfRangeException(name,
                "Enter a quantity from 0 to 1,000,000.");
    }

    private static void Percent(decimal value, decimal maximum, string name)
    {
        if (value < 0 || value > maximum || decimal.Round(value, 2) != value)
            throw new ArgumentOutOfRangeException(name,
                "Enter a percentage within the supported range with at most two decimal places.");
    }
}
