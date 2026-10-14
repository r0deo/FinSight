using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FinSight.Api.Website.Localization;
using FinSight.Application.Features.BusinessCosts;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FinSight.Api.Website.Models;

public sealed class BusinessCostForm
{
    [Required(ErrorMessage = "{0} is required."), StringLength(100, ErrorMessage = "{0} must not be more than {1} characters."), Display(Name = "Name")] public string Name { get; set; } = "";
    public BusinessCostCategory Category { get; set; }
    [Required(ErrorMessage = "{0} is required."), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? Amount { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "month")] public string Month { get; set; } = "";
    [Display(Name = "Actual or estimated")] public CostCertainty Certainty { get; set; }
    [Display(Name = "Already included in product unit costs?")] public UnitCostInclusion Inclusion { get; set; } = UnitCostInclusion.Unsure;
    [StringLength(500, ErrorMessage = "{0} must not be more than {1} characters."), Display(Name = "Note (optional)")] public string? Note { get; set; }
    public long Version { get; set; }
    public static bool TryMonth(string? value, out DateOnly month) =>
        DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out month)
        && CostDetailsValidator.ValidMonth(month);
    public CostDetails Details()
    {
        TryMonth(Month, out var month);
        return new(Name, Category, Amount!.Value, month, Certainty, Inclusion, Note);
    }
}
public sealed record BusinessCostsViewModel(BusinessCostSummary Summary, string Currency)
{
    public BusinessCostSummary? Previous { get; init; }
    public CostMonthComparison Comparison => new(Summary, Previous);
    public CostAdjustmentForm AdjustmentForm { get; init; } = new();
    public CostAdjustmentResult? Adjustment { get; init; }
}
public sealed class CostAdjustmentForm
{
    public string Month { get; set; } = "";
    public BusinessCostCategory Category { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Assumed reduction (%)"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? Percent { get; set; }
    [BindNever] public IReadOnlyList<BusinessCostCategory> Categories { get; set; } = [];
}
public static class CostLabels
{
    public static string Category(BusinessCostCategory c) => c switch {
        BusinessCostCategory.Labor => "Labor / wages", BusinessCostCategory.Rent => "Rent",
        BusinessCostCategory.Energy => "Electricity / generator / fuel",
        BusinessCostCategory.WaterInternet => "Water / internet", BusinessCostCategory.Transport => "Transport",
        BusinessCostCategory.Maintenance => "Maintenance",
        BusinessCostCategory.Inventory => "Inventory / raw-material purchases", _ => "Other" };
}
