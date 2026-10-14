using FinSight.Application.Features.BusinessCosts;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FluentValidation;

namespace FinSight.UnitTests.Application.Features.BusinessCosts;

public sealed class MonthlyCostWorkspaceTests
{
    private static BusinessCostResponse Entry(decimal amount, BusinessCostCategory category = BusinessCostCategory.Rent,
        CostCertainty certainty = CostCertainty.Actual) => new(Guid.NewGuid(), "Cost", category, amount,
            new(2026, 10, 1), certainty, UnitCostInclusion.No, null, 1);
    private static BusinessCostSummary Summary(params BusinessCostResponse[] entries) => new(new(2026, 10, 1), entries);

    [Fact]
    public void TotalsAndAdjustment_SeparateInventoryAndEstimates_AndLeaveInputsUnchanged()
    {
        var summary = Summary(Entry(100), Entry(20, certainty: CostCertainty.Estimated), Entry(50, BusinessCostCategory.Energy),
            Entry(900, BusinessCostCategory.Inventory), Entry(40, BusinessCostCategory.Inventory, CostCertainty.Estimated));
        var before = summary.Entries.ToArray();
        Assert.Equal(150, summary.OperatingActual); Assert.Equal(20, summary.OperatingEstimated);
        Assert.Equal(900, summary.InventoryActual); Assert.Equal(40, summary.InventoryEstimated);
        Assert.True(summary.HasEstimates);
        var result = PreviewCostAdjustmentHandler.Calculate(summary, BusinessCostCategory.Rent, 25);
        Assert.Equal(120, result.CategoryAmount); Assert.Equal(30, result.PotentialSaving);
        Assert.Equal(140, result.RevisedRecordedTotal); Assert.Equal(before, summary.Entries);
        Assert.Equal(170, summary.RecordedOperatingTotal);
        Assert.Equal(BusinessCostCategory.Rent, summary.OperatingCategories[0].Category);
    }
    [Fact]
    public void Comparison_MissingMonthIsUnknown_NotZero()
    {
        foreach (var comparison in new[] { new CostMonthComparison(Summary(Entry(100)), null),
            new CostMonthComparison(Summary(Entry(100)), Summary()), new CostMonthComparison(Summary(), Summary(Entry(100))) })
        {
            Assert.False(comparison.CanCompare); Assert.Null(comparison.OperatingChange);
            Assert.Null(comparison.OperatingPercent); Assert.Null(comparison.InventoryChange); Assert.True(comparison.NeedsCaution);
        }
    }
    [Fact]
    public void Comparison_ZeroPreviousAmountHasNoPercentage()
    {
        var comparison = new CostMonthComparison(Summary(Entry(100)), Summary(Entry(0)));
        Assert.True(comparison.CanCompare); Assert.Equal(100, comparison.OperatingChange); Assert.Null(comparison.OperatingPercent);
    }
    [Theory]
    [InlineData(false, false, false, false, true)]
    [InlineData(true, false, false, false, true)]
    [InlineData(false, true, false, false, true)]
    [InlineData(true, true, true, false, true)]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, true, false, false, false)]
    public void Comparison_WarnsWhenEitherMonthIsIncompleteOrEstimated(bool currentReviewed, bool previousReviewed,
        bool currentEstimated, bool previousEstimated, bool caution)
    {
        var current = Summary(Entry(150, certainty: currentEstimated ? CostCertainty.Estimated : CostCertainty.Actual))
            with { ReviewedAtUtc = currentReviewed ? DateTime.UtcNow : null };
        var previous = Summary(Entry(100, certainty: previousEstimated ? CostCertainty.Estimated : CostCertainty.Actual))
            with { ReviewedAtUtc = previousReviewed ? DateTime.UtcNow : null };
        var comparison = new CostMonthComparison(current, previous);
        Assert.Equal(caution, comparison.NeedsCaution); Assert.Equal(50, comparison.OperatingChange); Assert.Equal(50, comparison.OperatingPercent);
    }
    [Theory]
    [InlineData(-1)] [InlineData(101)] [InlineData(0.001)]
    public void Adjustment_InvalidPercentIsRejected(double percent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PreviewCostAdjustmentHandler.Calculate(Summary(Entry(100)), BusinessCostCategory.Rent, (decimal)percent));
        Assert.False(new PreviewCostAdjustmentValidator().Validate(new PreviewCostAdjustmentQuery(new(2026, 10, 1), BusinessCostCategory.Rent, (decimal)percent)).IsValid);
    }
    [Fact]
    public void Adjustment_RejectsInventoryUndefinedAndUnrecordedCategories()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PreviewCostAdjustmentHandler.Calculate(Summary(Entry(100)), BusinessCostCategory.Inventory, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => PreviewCostAdjustmentHandler.Calculate(Summary(Entry(100)), (BusinessCostCategory)999, 10));
        Assert.Throws<ValidationException>(() => PreviewCostAdjustmentHandler.Calculate(Summary(), BusinessCostCategory.Rent, 10));
    }
    [Fact]
    public void Adjustment_RoundsMoneyAndSupportsZeroAndFullReduction()
    {
        var summary = Summary(Entry(12.55m));
        Assert.Equal(1.26m, PreviewCostAdjustmentHandler.Calculate(summary, BusinessCostCategory.Rent, 10).PotentialSaving);
        Assert.Equal(12.55m, PreviewCostAdjustmentHandler.Calculate(summary, BusinessCostCategory.Rent, 0).RevisedRecordedTotal);
        Assert.Equal(0, PreviewCostAdjustmentHandler.Calculate(summary, BusinessCostCategory.Rent, 100).RevisedRecordedTotal);
    }
    [Fact]
    public void Review_RecordsOwnerAndUtcTime_ReopeningClearsDeclarationAndAdvancesVersion()
    {
        var month = new BusinessCostMonth(Guid.NewGuid(), new(2026, 10, 1));
        var owner = Guid.NewGuid(); var utc = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        Assert.Null(month.ReviewedAtUtc);
        month.Review(owner, utc);
        Assert.Equal(utc, month.ReviewedAtUtc); Assert.Equal(owner, month.ReviewedByUserId); Assert.Equal(2, month.Version);
        month.Reopen(); Assert.Null(month.ReviewedAtUtc); Assert.Null(month.ReviewedByUserId); Assert.Equal(3, month.Version);
        month.Reopen(); Assert.Equal(4, month.Version);
        Assert.Throws<ArgumentException>(() => month.Review(owner, DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => month.Review(Guid.Empty, utc));
    }
    [Fact]
    public void Review_ValidatesMonthVersionAndHasNoTenantInput()
    {
        var validator = new ReviewBusinessCostMonthValidator();
        Assert.False(validator.Validate(new ReviewBusinessCostMonthCommand(new(2026, 10, 2), 0, true)).IsValid);
        Assert.False(validator.Validate(new ReviewBusinessCostMonthCommand(new(2026, 10, 1), -1, true)).IsValid);
        Assert.Null(typeof(ReviewBusinessCostMonthCommand).GetProperty("BusinessId"));
        Assert.Null(typeof(PreviewCostAdjustmentQuery).GetProperty("BusinessId"));
    }
}
