using System.ComponentModel.DataAnnotations;
using FinSight.Api.Website.Localization;
using Microsoft.AspNetCore.Mvc;
using FinSight.Application.Features.Products.Queries.GetProducts;

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
}

public sealed record ProductListViewModel(GetProductsResponse Products, bool IncludeInactive);

public sealed record WebsiteErrorViewModel(int StatusCode, string Title, string Message);
