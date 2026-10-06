using FinSight.Domain.Enums;
using FinSight.Domain.Services;

namespace FinSight.Domain.Entities;

public sealed class PricingSimulation
{
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
        PricingSimulationCalculator calculator)
    {
        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
        ArgumentNullException.ThrowIfNull(calculator);

        if (updatedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updatedAtUtc),
                "Updated timestamp cannot be before the creation timestamp.");
        }

        Recalculate(
            proposedPrice,
            currentSalesQuantity,
            expectedSalesQuantity,
            updatedAtUtc,
            calculator);
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

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
        }
    }
}
