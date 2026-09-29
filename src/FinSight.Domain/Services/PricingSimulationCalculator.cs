using FinSight.Domain.Enums;
using FinSight.Domain.Models;

namespace FinSight.Domain.Services;

public sealed class PricingSimulationCalculator
{
    public PricingSimulationResult Calculate(
        decimal currentPrice,
        decimal proposedPrice,
        decimal unitCost,
        int currentSalesQuantity,
        int expectedSalesQuantity)
    {
        ThrowIfNegative(currentPrice, nameof(currentPrice));
        ThrowIfNegative(proposedPrice, nameof(proposedPrice));
        ThrowIfNegative(unitCost, nameof(unitCost));
        ThrowIfNegative(currentSalesQuantity, nameof(currentSalesQuantity));
        ThrowIfNegative(expectedSalesQuantity, nameof(expectedSalesQuantity));

        var currentContribution =
            (currentPrice - unitCost) * currentSalesQuantity;

        var proposedContribution =
            (proposedPrice - unitCost) * expectedSalesQuantity;

        var difference = proposedContribution - currentContribution;
        var isBelowCost = proposedPrice < unitCost;
        var recommendation = difference > 0
            ? Recommendation.Recommended
            : Recommendation.NotRecommended;

        return new PricingSimulationResult(
            currentContribution,
            proposedContribution,
            difference,
            isBelowCost,
            recommendation);
    }

    private static void ThrowIfNegative(decimal value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Value cannot be negative.");
        }
    }

    private static void ThrowIfNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "Value cannot be negative.");
        }
    }
}
