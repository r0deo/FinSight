using System.ComponentModel.DataAnnotations;
using FinSight.Api.Website.Localization;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace FinSight.Api.Website.Models;

public sealed class DiscountViewModel : CostLinkForm
{
    public Guid ProductId { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Discount percentage (%)")]
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? DiscountPercentage { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Current sales quantity")]
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? CurrentSalesQuantity { get; set; }
    [Display(Name = "Expected sales quantity"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? ExpectedSalesQuantity { get; set; }
    [Display(Name = "Additional campaign cost (optional)"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? CampaignCost { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Comparison period")]
    public SalesPeriod? ComparisonPeriod { get; set; } = SalesPeriod.Week;
    public long Version { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? PreviewCurrentPrice { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? PreviewUnitCost { get; set; }
    [BindNever, ValidateNever] public Guid? SimulationId { get; set; }
    [BindNever, ValidateNever] public string ProductName { get; set; } = "";
    [BindNever, ValidateNever] public string Currency { get; set; } = "";
    [BindNever, ValidateNever] public decimal CurrentPrice { get; set; }
    [BindNever, ValidateNever] public decimal UnitCost { get; set; }
    [BindNever, ValidateNever] public DiscountSimulationSnapshot? Result { get; set; }
}

public sealed record DiscountDisplayViewModel(DiscountSimulationSnapshot Snapshot, SalesPeriod Period, string Currency);
