using System.ComponentModel.DataAnnotations;
using FinSight.Api.Website.Localization;
using Microsoft.AspNetCore.Mvc;
using FinSight.Application.Features.Products.Queries.GetProducts;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using FinSight.Domain.Models;

namespace FinSight.Api.Website.Models;

public sealed class ProductFormViewModel
{
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Product name")]
    public string Name { get; set; } = string.Empty;
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Category")]
    public string Category { get; set; } = string.Empty;
    [Display(Name = "Size")]
    public string? Size { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Selling price"),
        ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? SellingPrice { get; set; }
    [Required(ErrorMessage = "{0} is required."), Display(Name = "Unit cost"),
        ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? UnitCost { get; set; }
    [ValidateNever] public ProductCostForm CostHelper { get; set; } = new();
    public bool UseCostHelper { get; set; }
    public bool ClearCostBreakdown { get; set; }
    public bool HelperOpen { get; set; }
    public string? CostPreviewFingerprint { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? UnitCostBeforeHelper { get; set; }
    [BindNever, ValidateNever] public ProductCostBreakdown? CostPreview { get; set; }
    [BindNever] public string Currency { get; set; } = "";
}

public sealed record ProductListViewModel(GetProductsResponse Products, bool IncludeInactive);

public sealed record WebsiteErrorViewModel(int StatusCode, string Title, string Message);
