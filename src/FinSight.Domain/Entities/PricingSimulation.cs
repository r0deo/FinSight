using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Services;
using FinSight.Domain.Models;

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

    public int? ExpectedSalesQuantity { get; private set; }

    public SalesPeriod? ComparisonPeriod { get; private set; }

    public int? DeclinePercentage { get; private set; }

    public string? CurrencySnapshot { get; private set; }

    public string? ProductNameSnapshot { get; private set; }

    public int CalculationVersion { get; private set; }

    public DiscountSimulationSnapshot? DiscountSnapshot { get; private set; }
    public DeliverySimulationSnapshot? DeliverySnapshot { get; private set; }

    public decimal CurrentContribution { get; private set; }

    public decimal? ProposedContribution { get; private set; }

    public decimal? Difference { get; private set; }

    public bool IsProposedPriceBelowCost { get; private set; }

    public Recommendation? Recommendation { get; private set; }

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

    public PricingSimulation(
        Guid id,
        Guid businessId,
        Guid productId,
        Guid createdByUserId,
        decimal currentPrice,
        decimal unitCost,
        decimal proposedPrice,
        int currentSalesQuantity,
        int? expectedSalesQuantity,
        SalesPeriod comparisonPeriod,
        int declinePercentage,
        string currencySnapshot,
        string productNameSnapshot,
        DateTime createdAtUtc,
        PricingDecisionCalculator decisionCalculator)
    {
        ThrowIfEmpty(id, nameof(id));
        ThrowIfEmpty(businessId, nameof(businessId));
        ThrowIfEmpty(productId, nameof(productId));
        ThrowIfEmpty(createdByUserId, nameof(createdByUserId));
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        ArgumentNullException.ThrowIfNull(decisionCalculator);
        if (!Enum.IsDefined(comparisonPeriod))
            throw new ArgumentOutOfRangeException(nameof(comparisonPeriod));
        if (string.IsNullOrWhiteSpace(currencySnapshot) || currencySnapshot.Trim().Length != Business.CurrencyLength)
            throw new ArgumentException("A three-letter currency is required.", nameof(currencySnapshot));
        if (string.IsNullOrWhiteSpace(productNameSnapshot))
            throw new ArgumentException("A product name is required.", nameof(productNameSnapshot));

        var decision = decisionCalculator.Calculate(currentPrice, proposedPrice, unitCost,
            currentSalesQuantity, expectedSalesQuantity, declinePercentage);
        Id = id;
        BusinessId = businessId;
        ProductId = productId;
        CreatedByUserId = createdByUserId;
        CurrentPrice = currentPrice;
        UnitCost = unitCost;
        ProposedPrice = proposedPrice;
        CurrentSalesQuantity = currentSalesQuantity;
        ExpectedSalesQuantity = expectedSalesQuantity;
        ComparisonPeriod = comparisonPeriod;
        DeclinePercentage = declinePercentage;
        CurrencySnapshot = currencySnapshot.Trim().ToUpperInvariant();
        ProductNameSnapshot = productNameSnapshot.Trim();
        CalculationVersion = 1;
        CurrentContribution = decision.CurrentContribution;
        ProposedContribution = decision.ExpectedScenario?.Contribution;
        Difference = decision.ExpectedScenario?.Difference;
        IsProposedPriceBelowCost = decision.IsProposedPriceBelowCost;
        Recommendation = decision.ExpectedScenario is { } expected
            ? expected.Difference > 0 ? FinSight.Domain.Enums.Recommendation.Recommended
                : FinSight.Domain.Enums.Recommendation.NotRecommended
            : null;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        Version = 1;
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

        if (DiscountSnapshot is not null)
            throw new PricingSimulationConflictException("Use the discount editor to change a discount simulation.");
        if (DeliverySnapshot is not null)
            throw new PricingSimulationConflictException("Use the delivery editor to change a delivery simulation.");

        Recalculate(
            proposedPrice,
            currentSalesQuantity,
            expectedSalesQuantity,
            updatedAtUtc,
            calculator);
        Version++;
    }

    public static PricingSimulation CreateDiscount(Guid id, Guid businessId, Guid productId,
        Guid userId, DiscountInputs inputs, SalesPeriod period, string currency, string productName,
        DateTime utcNow, DiscountSimulationCalculator calculator)
    {
        ThrowIfEmpty(id, nameof(id)); ThrowIfEmpty(businessId, nameof(businessId));
        ThrowIfEmpty(productId, nameof(productId)); ThrowIfEmpty(userId, nameof(userId));
        EnsureUtc(utcNow, nameof(utcNow));
        if (!Enum.IsDefined(period)) throw new ArgumentOutOfRangeException(nameof(period));
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != Business.CurrencyLength)
            throw new ArgumentException("A three-letter currency is required.", nameof(currency));
        if (string.IsNullOrWhiteSpace(productName) || productName.Trim().Length > Product.MaxNameLength)
            throw new ArgumentException("A product name is required.", nameof(productName));
        var snapshot = calculator.Calculate(inputs);
        var simulation = new PricingSimulation
        {
            Id = id, BusinessId = businessId, ProductId = productId, CreatedByUserId = userId,
            CurrentPrice = inputs.CurrentPrice, UnitCost = inputs.UnitCost,
            CurrencySnapshot = currency.Trim().ToUpperInvariant(), ProductNameSnapshot = productName.Trim(),
            CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow, Version = 1, CalculationVersion = 1
        };
        simulation.ApplyDiscount(snapshot, period);
        return simulation;
    }

    public void UpdateDiscount(decimal percentage, int currentQuantity, int? expectedQuantity,
        decimal? campaignCost, SalesPeriod period, DateTime utcNow, long expectedVersion,
        DiscountSimulationCalculator calculator)
    {
        EnsureExpectedVersion(expectedVersion); EnsureMutable(); EnsureValidUpdateTimestamp(utcNow);
        if (DiscountSnapshot is null) throw new PricingSimulationConflictException("This is not a discount simulation.");
        if (!Enum.IsDefined(period)) throw new ArgumentOutOfRangeException(nameof(period));
        var snapshot = calculator.Calculate(new(CurrentPrice, UnitCost, percentage, currentQuantity, expectedQuantity, campaignCost));
        ApplyDiscount(snapshot, period);
        Touch(utcNow);
    }

    private void ApplyDiscount(DiscountSimulationSnapshot snapshot, SalesPeriod period)
    {
        DiscountSnapshot = snapshot;
        ProposedPrice = snapshot.Result.DiscountedPrice;
        CurrentSalesQuantity = snapshot.Inputs.CurrentSalesQuantity;
        ExpectedSalesQuantity = snapshot.Inputs.ExpectedSalesQuantity;
        ComparisonPeriod = period;
        CurrentContribution = snapshot.Result.CurrentContribution;
        ProposedContribution = snapshot.Result.ScenarioContribution;
        Difference = snapshot.Result.Difference;
        IsProposedPriceBelowCost = snapshot.Result.IsBelowCost;
        Recommendation = Difference.HasValue ? Difference > 0
            ? FinSight.Domain.Enums.Recommendation.Recommended : FinSight.Domain.Enums.Recommendation.NotRecommended : null;
    }

    public static PricingSimulation CreateDelivery(Guid id, Guid businessId, Guid productId, Guid userId,
        decimal currentPrice, decimal unitCost, DeliveryAssumptions inputs, string currency, string productName,
        DateTime utcNow, DeliverySimulationCalculator calculator)
    {
        ThrowIfEmpty(id, nameof(id)); ThrowIfEmpty(businessId, nameof(businessId));
        ThrowIfEmpty(productId, nameof(productId)); ThrowIfEmpty(userId, nameof(userId));
        EnsureUtc(utcNow, nameof(utcNow)); ArgumentNullException.ThrowIfNull(calculator);
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != Business.CurrencyLength)
            throw new ArgumentException("A three-letter currency is required.", nameof(currency));
        if (string.IsNullOrWhiteSpace(productName) || productName.Trim().Length > Product.MaxNameLength)
            throw new ArgumentException("A product name is required.", nameof(productName));
        var snapshot = calculator.Calculate(currentPrice, unitCost, inputs);
        var simulation = new PricingSimulation
        {
            Id = id, BusinessId = businessId, ProductId = productId, CreatedByUserId = userId,
            CurrentPrice = currentPrice, UnitCost = unitCost, CurrencySnapshot = currency.Trim().ToUpperInvariant(),
            ProductNameSnapshot = productName.Trim(), CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow,
            Version = 1, CalculationVersion = 1
        };
        simulation.ApplyDelivery(snapshot);
        return simulation;
    }

    public void UpdateDelivery(DeliveryAssumptions inputs, DateTime utcNow, long expectedVersion, DeliverySimulationCalculator calculator)
    {
        EnsureExpectedVersion(expectedVersion); EnsureMutable(); EnsureValidUpdateTimestamp(utcNow);
        ArgumentNullException.ThrowIfNull(calculator);
        if (DeliverySnapshot is null) throw new PricingSimulationConflictException("This is not a delivery simulation.");
        ApplyDelivery(calculator.Calculate(CurrentPrice, UnitCost, inputs));
        Touch(utcNow);
    }

    private void ApplyDelivery(DeliverySimulationSnapshot snapshot)
    {
        DeliverySnapshot = snapshot;
        ComparisonPeriod = snapshot.Inputs.ComparisonPeriod;
        ProposedPrice = snapshot.Inputs.DeliveryUnitPrice;
        // Legacy columns are never used to display delivery results. The dedicated snapshot
        // preserves nullable order counts/totals instead of inventing a period comparison.
        CurrentSalesQuantity = snapshot.Inputs.CurrentDirectOrders ?? 0;
        ExpectedSalesQuantity = snapshot.Inputs.ExpectedDeliveryOrders;
        CurrentContribution = snapshot.Result.BaselineContribution ?? 0;
        ProposedContribution = snapshot.Result.ScenarioContribution;
        Difference = snapshot.Result.Difference;
        IsProposedPriceBelowCost = snapshot.Inputs.DeliveryUnitPrice < UnitCost;
        Recommendation = Difference.HasValue ? Difference > 0
            ? FinSight.Domain.Enums.Recommendation.Recommended : FinSight.Domain.Enums.Recommendation.NotRecommended : null;
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
