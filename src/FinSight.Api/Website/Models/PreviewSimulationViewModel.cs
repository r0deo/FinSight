using System.ComponentModel.DataAnnotations;
using FinSight.Api.Website.Localization;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace FinSight.Api.Website.Models;

public sealed class PreviewSimulationViewModel : CostLinkForm
{
    [Display(Name = "Additional decision cost for this period (optional)"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? AdditionalDecisionCost { get; set; }
    public Guid ProductId { get; set; }

    [Display(Name = "Proposed price"), Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "{0} cannot be negative."), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal ProposedPrice { get; set; }

    [Display(Name = "Current sales quantity"), Range(0, int.MaxValue,
        ErrorMessage = "{0} cannot be negative."), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int CurrentSalesQuantity { get; set; }

    [Display(Name = "Expected sales quantity"), Range(0, int.MaxValue,
        ErrorMessage = "{0} cannot be negative."), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? ExpectedSalesQuantity { get; set; }

    public bool UnknownExpectedSales { get; set; }

    [Display(Name = "Comparison period")]
    public SalesPeriod ComparisonPeriod { get; set; } = SalesPeriod.Week;

    [Display(Name = "Illustrative sales decline (%)"), Range(0, 100,
        ErrorMessage = "{0} must be between {1} and {2}.")]
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? DeclinePercentage { get; set; } = 10;

    [BindNever, ValidateNever] public string ProductName { get; set; } = string.Empty;
    [BindNever, ValidateNever] public decimal CurrentPrice { get; set; }
    [BindNever, ValidateNever] public decimal UnitCost { get; set; }
    [BindNever, ValidateNever] public string Currency { get; set; } = string.Empty;
    [BindNever, ValidateNever] public PricingDecisionResult? Result { get; set; }
}

public sealed class SaveDecisionViewModel : CostLinkForm
{
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? AdditionalDecisionCost { get; set; }
    public Guid ProductId { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal ProposedPrice { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int CurrentSalesQuantity { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? ExpectedSalesQuantity { get; set; }
    public SalesPeriod ComparisonPeriod { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int DeclinePercentage { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal PreviewCurrentPrice { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal PreviewUnitCost { get; set; }
}
