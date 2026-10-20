using FinSight.Domain.Models;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;

namespace FinSight.Domain.Services;

public static class SimulationCostCalculator
{
    public static bool Eligible(BusinessCostCategory category, UnitCostInclusion inclusion) =>
        inclusion == UnitCostInclusion.No && category is BusinessCostCategory.Rent or BusinessCostCategory.Labor
            or BusinessCostCategory.Energy or BusinessCostCategory.WaterInternet;

    public static CostLinkOptions Validate(CostLinkOptions? options)
    {
        var settings = (options ?? new()).Normalize();
        if (!settings.Enabled) return settings;
        if (settings.Month is not { Day: 1, Year: >= 2000 and <= 2100 })
            throw new CostContextValidationException("CostMonth", "Choose a month between 2000 and 2100.");
        if (settings.Allocate && (settings.Percentage is null or < 0 or > 100
            || decimal.Round(settings.Percentage.Value, 2) != settings.Percentage))
            throw new CostContextValidationException("AllocationPercentage", "Enter an allocation percentage from 0 to 100 with at most two decimal places.");
        return settings;
    }

    public static SimulationCostContext Calculate(CostLinkOptions? options, SalesPeriod period,
        IReadOnlyList<LinkedCostEntry> entries, bool reviewed, decimal? current, IReadOnlyList<ContributionOption> proposed)
    {
        var settings = Validate(options);
        if (!Enum.IsDefined(period)) throw new ArgumentOutOfRangeException(nameof(period));
        // Disabled values cannot influence calculations, even if submitted by a manipulated client.
        if (!settings.Enabled) return new(settings, period, [], false, false, null, "Excluded", null, current, null,
            proposed.Select(x => new AllocatedOption(x.Label, x.Contribution, null)).ToArray(), "Excluded");
        try
        {
            var classified = entries.Select(x => x with { Eligible = Eligible(x.Category, x.Inclusion) }).ToArray();
            var eligible = classified.Where(x => x.Eligible).ToArray();
            decimal? basis = eligible.Length == 0 ? null : eligible.Sum(x => x.Amount);
            var limitation = entries.Count == 0 ? "NoRecords" : basis is null ? "NoEligibleCosts"
                : period != SalesPeriod.Month ? "PeriodMismatch" : current is null ? "NoPeriodComparison"
                : settings.Allocate && !settings.ConfirmBasis ? "UnconfirmedBasis" : "";
            decimal? allocation = settings.Allocate && limitation.Length == 0
                ? decimal.Round(basis!.Value * settings.Percentage!.Value / 100, 2, MidpointRounding.AwayFromZero) : null;
            var rows = proposed.Select(x => new AllocatedOption(x.Label, x.Contribution,
                allocation.HasValue ? x.Contribution - allocation : null)).ToArray();
            var remainder = allocation.HasValue ? current - allocation : null;
            foreach (var amount in rows.Select(x => x.Remainder).Append(remainder).Append(basis))
                if (amount.HasValue && Math.Abs(amount.Value) > DeliverySimulationCalculator.MaximumMoney) throw new OverflowException();
            return new(settings, period, classified, reviewed, classified.Any(x => x.Certainty == CostCertainty.Estimated),
                basis, period == SalesPeriod.Month ? "ExactRecordedMonth" : "ContextOnlyNoConversion",
                allocation, current, remainder, rows, limitation);
        }
        catch (OverflowException) { throw new CostContextValidationException("", "These amounts exceed the supported calculation range."); }
    }

    public static SimulationCostContext Recalculate(SimulationCostContext snapshot, decimal? current, IReadOnlyList<ContributionOption> options) =>
        Calculate(snapshot.Settings, snapshot.Period, snapshot.Entries, snapshot.MonthReviewed, current, options);
}
