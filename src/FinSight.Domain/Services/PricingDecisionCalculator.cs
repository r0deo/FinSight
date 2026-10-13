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
        int? declinePercentage)
    {
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
        var status = MaintenanceThresholdStatus.Available;
        int? minimum = null;

        if (currentContribution <= 0)
            status = MaintenanceThresholdStatus.NoPositiveBaseline;
        else if (proposedUnitContribution <= 0)
            status = MaintenanceThresholdStatus.ImpossibleAtPrice;
        else if (currentContribution / int.MaxValue > proposedUnitContribution)
            status = MaintenanceThresholdStatus.OutsideSupportedQuantity;
        else
            minimum = (int)decimal.Ceiling(currentContribution / proposedUnitContribution);

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
            baseline.IsBelowCost, scenarios);

        void AddScenario(PricingScenarioKind kind, int quantity)
        {
            try
            {
                var result = calculator.Calculate(currentPrice, proposedPrice, unitCost,
                    currentSalesQuantity, quantity);
                scenarios.Add(new PricingScenarioResult(kind, quantity,
                    proposedPrice * quantity, unitCost * quantity,
                    result.ProposedContribution, result.Difference));
            }
            catch (OverflowException)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity),
                    "Values exceed the supported calculation range.");
            }
        }
    }
}
