using FinSight.Domain.Enums;
using FinSight.Domain.Models;

namespace FinSight.Domain.Services;

public sealed class PricingDecisionCalculator(PricingSimulationCalculator calculator)
{
    public PricingDecisionResult Calculate(
        decimal currentPrice,
        decimal proposedPrice,
        decimal unitCost,
        int currentSalesQuantity,
        int? expectedSalesQuantity,
        int? declinePercentage, decimal? additionalDecisionCost = null)
    {
        if (additionalDecisionCost is < 0 or > DeliverySimulationCalculator.MaximumMoney
            || (additionalDecisionCost.HasValue && decimal.Round(additionalDecisionCost.Value, 2) != additionalDecisionCost))
            throw new ArgumentOutOfRangeException(nameof(additionalDecisionCost));
        if (declinePercentage is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(declinePercentage));
        if (expectedSalesQuantity is < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedSalesQuantity));

        // The original calculator remains the single contribution rule for every row.
        PricingSimulationResult baseline;
        try
        {
            baseline = calculator.Calculate(currentPrice, proposedPrice, unitCost,
                currentSalesQuantity, currentSalesQuantity);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(currentSalesQuantity),
                "Values exceed the supported calculation range.");
        }
        var currentContribution = baseline.CurrentContribution;
        var proposedUnitContribution = proposedPrice - unitCost;
        var (status, minimum) = MaintenanceThreshold(currentContribution > 0 ? currentContribution + (additionalDecisionCost ?? 0) : currentContribution, proposedUnitContribution);

        var scenarios = new List<PricingScenarioResult>(3);
        AddScenario(PricingScenarioKind.SameSales, currentSalesQuantity);
        if (declinePercentage.HasValue)
        {
            var reduced = (int)decimal.Floor((decimal)currentSalesQuantity
                * (100 - declinePercentage.Value) / 100);
            AddScenario(PricingScenarioKind.LowerSales, reduced);
        }
        if (expectedSalesQuantity.HasValue)
            AddScenario(PricingScenarioKind.ExpectedSales, expectedSalesQuantity.Value);

        return new PricingDecisionResult(currentContribution, status, minimum,
            minimum - currentSalesQuantity,
            minimum.HasValue && currentSalesQuantity > 0
                ? (decimal)(minimum.Value - currentSalesQuantity) * 100 / currentSalesQuantity
                : null,
            baseline.IsBelowCost, scenarios) { AdditionalDecisionCost = additionalDecisionCost };

        void AddScenario(PricingScenarioKind kind, int quantity)
        {
            try
            {
                var result = calculator.Calculate(currentPrice, proposedPrice, unitCost,
                    currentSalesQuantity, quantity);
                scenarios.Add(new PricingScenarioResult(kind, quantity,
                    proposedPrice * quantity, unitCost * quantity,
                    result.ProposedContribution - (additionalDecisionCost ?? 0), result.Difference - (additionalDecisionCost ?? 0)));
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity),
                    "Values exceed the supported calculation range.");
            }
        }
    }

    public static (MaintenanceThresholdStatus Status, int? MinimumUnits) MaintenanceThreshold(
        decimal baselineContribution, decimal optionUnitContribution,
        int maximumQuantity = int.MaxValue)
    {
        if (baselineContribution <= 0)
            return (MaintenanceThresholdStatus.NoPositiveBaseline, null);
        if (optionUnitContribution <= 0)
            return (MaintenanceThresholdStatus.ImpossibleAtPrice, null);
        if (baselineContribution / maximumQuantity > optionUnitContribution)
            return (MaintenanceThresholdStatus.OutsideSupportedQuantity, null);
        return (MaintenanceThresholdStatus.Available,
            (int)decimal.Ceiling(baselineContribution / optionUnitContribution));
    }
}
