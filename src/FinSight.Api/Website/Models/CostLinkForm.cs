using System.ComponentModel.DataAnnotations;
using FinSight.Api.Website.Localization;
using FinSight.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace FinSight.Api.Website.Models;

public class CostLinkForm
{
    public bool LinkCosts { get; set; }
    public string? CostMonth { get; set; }
    public bool AllocateCosts { get; set; }
    [Display(Name = "Product allocation (%)"), ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))]
    public decimal? AllocationPercentage { get; set; }
    public bool ConfirmCostBasis { get; set; }
    public string? CostBasis { get; set; }
    [BindNever, ValidateNever] public SimulationCostContext? CostContext { get; set; }

    public CostLinkOptions CostOptions() => !LinkCosts ? new() : new(true,
        BusinessCostForm.TryMonth(CostMonth, out var month) ? month : null, AllocateCosts,
        AllocateCosts ? AllocationPercentage : null, AllocateCosts && ConfirmCostBasis);

    public void RestoreCostSettings(SimulationCostContext? snapshot)
    {
        var settings = snapshot?.Settings ?? new();
        LinkCosts = settings.Enabled; CostMonth = settings.Month?.ToString("yyyy-MM");
        AllocateCosts = settings.Allocate; AllocationPercentage = settings.Percentage;
        ConfirmCostBasis = settings.ConfirmBasis;
    }
}

public sealed record CostContextDisplayModel(SimulationCostContext? Context, string Currency);
