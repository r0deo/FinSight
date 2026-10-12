using System.ComponentModel.DataAnnotations;
using FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingSimulation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace FinSight.Api.Website.Models;

public sealed class PreviewSimulationViewModel
{
    public Guid ProductId { get; set; }
    [Display(Name = "Proposed price"), Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal ProposedPrice { get; set; }
    [Display(Name = "Current sales quantity"), Range(0, int.MaxValue)]
    public int CurrentSalesQuantity { get; set; }
    [Display(Name = "Expected sales quantity"), Range(0, int.MaxValue)]
    public int ExpectedSalesQuantity { get; set; }
    [BindNever, ValidateNever]
    public string ProductName { get; set; } = string.Empty;
    [BindNever, ValidateNever]
    public PreviewPricingSimulationResponse? Result { get; set; }
}
