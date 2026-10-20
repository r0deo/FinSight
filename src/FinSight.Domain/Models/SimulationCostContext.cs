using FinSight.Domain.Enums;

namespace FinSight.Domain.Models;

public sealed record CostLinkOptions(bool Enabled = false, DateOnly? Month = null,
    bool Allocate = false, decimal? Percentage = null, bool ConfirmBasis = false)
{
    public CostLinkOptions Normalize() => !Enabled ? new() : this with
    { Percentage = Allocate ? Percentage : null, ConfirmBasis = Allocate && ConfirmBasis };
}

public sealed record LinkedCostEntry(Guid Id, string Name, BusinessCostCategory Category, decimal Amount,
    CostCertainty Certainty, UnitCostInclusion Inclusion, long Version, bool Eligible);
public sealed record ContributionOption(string Label, decimal? Contribution);
public sealed record AllocatedOption(string Label, decimal? Contribution, decimal? Remainder);

// Null on old simulations means not captured, never zero. A captured disabled context has no expense data.
public sealed record SimulationCostContext(CostLinkOptions Settings, SalesPeriod Period,
    IReadOnlyList<LinkedCostEntry> Entries, bool MonthReviewed, bool HasEstimates, decimal? RecordedBasis,
    string ConversionBasis, decimal? AllocationAmount, decimal? CurrentContribution,
    decimal? CurrentRemainder, IReadOnlyList<AllocatedOption> Options, string Limitation)
{
    public string BasisFingerprint { get; init; } = "";
}
