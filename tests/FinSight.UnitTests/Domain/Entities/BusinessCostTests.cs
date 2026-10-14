using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Application.Features.BusinessCosts;
namespace FinSight.UnitTests.Domain.Entities;

public class BusinessCostTests
{
    [Fact]
    public void ValidCost_TrimsDetails_AndUpdateAdvancesVersion()
    {
        var cost = Cost();
        Assert.Equal("Rent", cost.Name); Assert.Equal(1, cost.Version);
        cost.Update(" Power ", BusinessCostCategory.Energy, 20.50m, new(2026, 11, 1),
            CostCertainty.Estimated, UnitCostInclusion.Yes, " included ");
        Assert.Equal("Power", cost.Name); Assert.Equal("included", cost.Note); Assert.Equal(2, cost.Version);
        Assert.Equal(UnitCostInclusion.Yes, cost.Inclusion);
    }
    [Theory]
    [InlineData(-1)] [InlineData(1.001)] [InlineData(1000000000000)]
    public void InvalidAmounts_DoNotMutate(decimal amount)
    {
        var cost = Cost();
        Assert.Throws<ArgumentOutOfRangeException>(() => cost.Update("Other", BusinessCostCategory.Other,
            amount, new(2026, 10, 1), CostCertainty.Actual, UnitCostInclusion.No, null));
        Assert.Equal(100, cost.Amount); Assert.Equal(1, cost.Version); Assert.Equal("Rent", cost.Name);
    }
    [Fact]
    public void Summary_SeparatesInventoryActualEstimates_AndKeepsUnresolvedVisible()
    {
        var summary = new BusinessCostSummary(new(2026, 10, 1), [
            new(Guid.NewGuid(), "Rent", BusinessCostCategory.Rent, 100, new(2026,10,1), CostCertainty.Actual, UnitCostInclusion.No, null, 1),
            new(Guid.NewGuid(), "Rent estimate", BusinessCostCategory.Rent, 20, new(2026,10,1), CostCertainty.Estimated, UnitCostInclusion.Yes, null, 1),
            new(Guid.NewGuid(), "Stock", BusinessCostCategory.Inventory, 300, new(2026,10,1), CostCertainty.Actual, UnitCostInclusion.Unsure, null, 1)]);
        var category = Assert.Single(summary.OperatingCategories);
        Assert.Equal(100, category.Actual); Assert.Equal(20, category.Estimated);
        Assert.Equal(300, summary.InventoryActual); Assert.Equal(0, summary.InventoryEstimated);
        Assert.Equal(1, summary.UnresolvedCount);
    }
    [Fact]
    public void Validation_RejectsInvalidMonthClassificationsNamesAndBounds()
    {
        var validator = new CostDetailsValidator();
        var valid = new CostDetails("Rent", BusinessCostCategory.Rent, 100, new(2026,10,1), CostCertainty.Actual, UnitCostInclusion.No, null);
        Assert.True(validator.Validate(valid).IsValid);
        foreach(var bad in new[] {
            valid with { Name = " " }, valid with { Name = new string('x',101) },
            valid with { Category = (BusinessCostCategory)99 }, valid with { Certainty = (CostCertainty)99 },
            valid with { Inclusion = (UnitCostInclusion)99 }, valid with { Amount = -1 },
            valid with { Month = new(2026,10,2) }, valid with { Month = new(1999,12,1) },
            valid with { Month = new(2101,1,1) }, valid with { Note = new string('x',501) } })
            Assert.False(validator.Validate(bad).IsValid);
    }
    private static BusinessCost Cost() => new(Guid.NewGuid(), Guid.NewGuid(), " Rent ", BusinessCostCategory.Rent,
        100, new(2026,10,1), CostCertainty.Actual, UnitCostInclusion.No, null);
}
