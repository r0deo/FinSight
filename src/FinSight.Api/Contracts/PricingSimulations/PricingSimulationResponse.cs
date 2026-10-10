using FinSight.Domain.Enums;

namespace FinSight.Api.Contracts.PricingSimulations;

public sealed record PricingSimulationResponse(
    Guid Id,
    Guid ProductId,
    Guid CreatedByUserId,
    decimal CurrentPrice,
    decimal UnitCost,
    decimal ProposedPrice,
    int CurrentSalesQuantity,
    int ExpectedSalesQuantity,
    decimal CurrentContribution,
    decimal ProposedContribution,
    decimal Difference,
    bool IsProposedPriceBelowCost,
    Recommendation Recommendation,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    bool IsArchived,
    DateTime? ArchivedAtUtc,
    long Version);
