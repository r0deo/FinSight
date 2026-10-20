using System.ComponentModel.DataAnnotations;
using FinSight.Api.Website.Localization;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace FinSight.Api.Website.Models;

public sealed class DeliveryViewModel : CostLinkForm
{
    public Guid ProductId { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Units of this product per order"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? UnitsPerOrder { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Delivery unit price"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? DeliveryUnitPrice { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Platform commission (%)"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? CommissionPercentage { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Additional packaging per order"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? PackagingPerOrder { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Other merchant-paid charges per order"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? OtherChargesPerOrder { get; set; }
    [Display(Name = "Additional fixed period cost (optional)"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? FixedPeriodCost { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Comparison period")]
    public SalesPeriod? ComparisonPeriod { get; set; } = SalesPeriod.Week;
    [Display(Name = "Current direct orders"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? CurrentDirectOrders { get; set; }
    [Display(Name = "Expected delivery orders"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? ExpectedDeliveryOrders { get; set; }
    [Display(Name = "Delivery orders transferred from direct sales"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? TransferredOrders { get; set; }
    public long Version { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? PreviewCurrentPrice { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? PreviewUnitCost { get; set; }
    [BindNever, ValidateNever] public Guid? SimulationId { get; set; }
    [BindNever, ValidateNever] public string ProductName { get; set; } = "";
    [BindNever, ValidateNever] public string Currency { get; set; } = "";
    [BindNever, ValidateNever] public decimal CurrentPrice { get; set; }
    [BindNever, ValidateNever] public decimal UnitCost { get; set; }
    [BindNever, ValidateNever] public DeliverySimulationSnapshot? Result { get; set; }
    public DeliveryAssumptions Inputs() => new(UnitsPerOrder!.Value, DeliveryUnitPrice!.Value, CommissionPercentage!.Value,
        PackagingPerOrder!.Value, OtherChargesPerOrder!.Value, FixedPeriodCost, ComparisonPeriod!.Value,
        CurrentDirectOrders, ExpectedDeliveryOrders, TransferredOrders);
}
public sealed record DeliveryDisplayViewModel(DeliverySimulationSnapshot Snapshot, string Currency);
