using System.ComponentModel.DataAnnotations;
using FinSight.Application.Features.Products.Queries.GetProducts;

namespace FinSight.Api.Website.Models;

public sealed class ProductFormViewModel
{
    [Required]
    public string Name { get; set; } = string.Empty;
    [Required]
    public string Category { get; set; } = string.Empty;
    public string? Size { get; set; }
    [Required, Display(Name = "Selling price")]
    public decimal? SellingPrice { get; set; }
    [Required, Display(Name = "Unit cost")]
    public decimal? UnitCost { get; set; }
}

public sealed record ProductListViewModel(GetProductsResponse Products, bool IncludeInactive);

public sealed record WebsiteErrorViewModel(int StatusCode, string Title, string Message);
