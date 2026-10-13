using System.ComponentModel.DataAnnotations;
using FinSight.Api.Website.Localization;
using FinSight.Application.Features.WhatIf;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace FinSight.Api.Website.Models;

public sealed class WhatIfFormViewModel
{
    public Guid ProductId { get; set; }
    [Required, Display(Name = "Current units sold")]
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? CurrentQuantity { get; set; }
    public SalesPeriod Period { get; set; } = SalesPeriod.Week;
    public CostShockInputMode CostInputMode { get; set; } = CostShockInputMode.Percentage;
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? CostIncreasePercent { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? NewUnitCost { get; set; }
    [Required, Display(Name = "Sales decline (%)"),
        ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? SalesDeclinePercent { get; set; }
    public bool UsePriceResponse { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? AlternativePrice { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? AlternativePriceQuantity { get; set; }
    public bool UseSupplierResponse { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? AlternativeSupplierCost { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public int? AlternativeSupplierQuantity { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal PreviewPrice { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal PreviewCost { get; set; }
    public string? PreviewProof { get; set; }
    [BindNever, ValidateNever] public string ProductName { get; set; } = string.Empty;
    [BindNever, ValidateNever] public string Currency { get; set; } = string.Empty;
    [BindNever, ValidateNever] public decimal CurrentPrice { get; set; }
    [BindNever, ValidateNever] public decimal CurrentUnitCost { get; set; }
    [BindNever, ValidateNever] public WhatIfResult? Result { get; set; }

    public WhatIfRequest ToRequest() => new(ProductId, CurrentQuantity!.Value, Period,
        CostInputMode, CostInputMode == CostShockInputMode.Percentage ? CostIncreasePercent : null,
        CostInputMode == CostShockInputMode.NewUnitCost ? NewUnitCost : null,
        SalesDeclinePercent!.Value, UsePriceResponse ? AlternativePrice : null,
        UsePriceResponse ? AlternativePriceQuantity : null,
        UseSupplierResponse ? AlternativeSupplierCost : null,
        UseSupplierResponse ? AlternativeSupplierQuantity : null);
}

public sealed record WhatIfDisplayViewModel(string ProductName, string Currency,
    SalesPeriod Period, WhatIfInputs Inputs, WhatIfResult Result, bool IsHistorical);
