using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Services;

namespace FinSight.Domain.Entities;

public sealed class PricingSimulation
{
    public static readonly TimeSpan RestoreWindow = TimeSpan.FromDays(30);

    public Guid Id { get; private set; }

    public Guid BusinessId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public decimal CurrentPrice { get; private set; }

    public decimal UnitCost { get; private set; }

    public decimal ProposedPrice { get; private set; }

    public int CurrentSalesQuantity { get; private set; }

    public int ExpectedSalesQuantity { get; private set; }

    public decimal CurrentContribution { get; private set; }

    public decimal ProposedContribution { get; private set; }

    public decimal Difference { get; private set; }

    public bool IsProposedPriceBelowCost { get; private set; }

    public Recommendation Recommendation { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public long Version { get; private set; }

    public bool IsArchived => ArchivedAtUtc.HasValue;

    public bool IsDeleted => DeletedAtUtc.HasValue;

    private PricingSimulation() { }

    public PricingSimulation(
        Guid id,
        Guid businessId,
        Guid productId,
        Guid createdByUserId,
        decimal currentPrice,
        decimal unitCost,
        decimal proposedPrice,
        int currentSalesQuantity,
        int expectedSalesQuantity,
        DateTime createdAtUtc,
        PricingSimulationCalculator calculator)
    {
        ThrowIfEmpty(id, nameof(id));
        ThrowIfEmpty(businessId, nameof(businessId));
        ThrowIfEmpty(productId, nameof(productId));
        ThrowIfEmpty(createdByUserId, nameof(createdByUserId));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        ArgumentNullException.ThrowIfNull(calculator);

        Id = id;
        BusinessId = businessId;
        ProductId = productId;
        CreatedByUserId = createdByUserId;
        CurrentPrice = currentPrice;
        UnitCost = unitCost;
        CreatedAtUtc = createdAtUtc;
        Version = 1;

        Recalculate(
            proposedPrice,
            currentSalesQuantity,
            expectedSalesQuantity,
            createdAtUtc,
            calculator);
    }

    public void Update(
        decimal proposedPrice,
        int currentSalesQuantity,
        int expectedSalesQuantity,
        DateTime updatedAtUtc,
        PricingSimulationCalculator calculator,
        long expectedVersion)
    {
        EnsureExpectedVersion(expectedVersion);
        EnsureMutable();
        EnsureValidUpdateTimestamp(updatedAtUtc);
        ArgumentNullException.ThrowIfNull(calculator);

        Recalculate(
            proposedPrice,
            currentSalesQuantity,
            expectedSalesQuantity,
            updatedAtUtc,
            calculator);
        Version++;
    }

    public void Archive(DateTime archivedAtUtc, long expectedVersion)
    {
        EnsureExpectedVersion(expectedVersion);
        EnsureNotDeleted();

        if (IsArchived)
        {
            throw new PricingSimulationConflictException(
                "The pricing simulation is already archived.");
        }

        EnsureValidUpdateTimestamp(archivedAtUtc);
        ArchivedAtUtc = archivedAtUtc;
        Touch(archivedAtUtc);
    }

    public void Unarchive(DateTime updatedAtUtc, long expectedVersion)
    {
        EnsureExpectedVersion(expectedVersion);
        EnsureNotDeleted();

        if (!IsArchived)
        {
            throw new PricingSimulationConflictException(
                "The pricing simulation is not archived.");
        }

        EnsureValidUpdateTimestamp(updatedAtUtc);
        ArchivedAtUtc = null;
        Touch(updatedAtUtc);
    }

    public void Delete(DateTime deletedAtUtc, long expectedVersion)
    {
        EnsureExpectedVersion(expectedVersion);

        if (IsDeleted)
        {
            throw new PricingSimulationConflictException(
                "The pricing simulation is already deleted.");
        }

        EnsureValidUpdateTimestamp(deletedAtUtc);
        DeletedAtUtc = deletedAtUtc;
        Touch(deletedAtUtc);
    }

    public void Restore(DateTime restoredAtUtc, long expectedVersion)
    {
        EnsureExpectedVersion(expectedVersion);

        if (!DeletedAtUtc.HasValue)
        {
            throw new PricingSimulationConflictException(
                "The pricing simulation is not deleted.");
        }

        EnsureValidUpdateTimestamp(restoredAtUtc);

        if (restoredAtUtc > DeletedAtUtc.Value.Add(RestoreWindow))
        {
            throw new PricingSimulationConflictException(
                "The 30-day restoration window has expired.");
        }

        DeletedAtUtc = null;
        Touch(restoredAtUtc);
    }

    public bool IsEligibleForPermanentDeletion(DateTime utcNow)
    {
        EnsureUtc(utcNow, nameof(utcNow));

        if (!DeletedAtUtc.HasValue)
        {
            return false;
        }

        // Restoration is still allowed at exactly 30 days after deletion.
        return utcNow > DeletedAtUtc.Value.Add(RestoreWindow);
    }

    private void Recalculate(
        decimal proposedPrice,
        int currentSalesQuantity,
        int expectedSalesQuantity,
        DateTime updatedAtUtc,
        PricingSimulationCalculator calculator)
    {
        var result = calculator.Calculate(
            CurrentPrice,
            proposedPrice,
            UnitCost,
            currentSalesQuantity,
            expectedSalesQuantity);

        ProposedPrice = proposedPrice;
        CurrentSalesQuantity = currentSalesQuantity;
        ExpectedSalesQuantity = expectedSalesQuantity;
        CurrentContribution = result.CurrentContribution;
        ProposedContribution = result.ProposedContribution;
        Difference = result.Difference;
        IsProposedPriceBelowCost = result.IsBelowCost;
        Recommendation = result.Recommendation;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static void ThrowIfEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("ID cannot be empty.", parameterName);
        }
    }

    private void EnsureExpectedVersion(long expectedVersion)
    {
        if (expectedVersion != Version)
        {
            throw new PricingSimulationConflictException(
                "The pricing simulation was changed by another request.");
        }
    }

    private void EnsureMutable()
    {
        EnsureNotDeleted();

        if (IsArchived)
        {
            throw new PricingSimulationConflictException(
                "An archived pricing simulation cannot be edited.");
        }
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new PricingSimulationConflictException(
                "A deleted pricing simulation cannot be changed by this operation.");
        }
    }

    private void Touch(DateTime updatedAtUtc)
    {
        EnsureValidUpdateTimestamp(updatedAtUtc);

        UpdatedAtUtc = updatedAtUtc;
        Version++;
    }

    private void EnsureValidUpdateTimestamp(DateTime updatedAtUtc)
    {
        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));

        if (updatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updatedAtUtc),
                "Updated timestamp cannot be before the creation timestamp.");
        }
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
        }
    }
}
