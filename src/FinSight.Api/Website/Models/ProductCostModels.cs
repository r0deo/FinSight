using FinSight.Api.Website.Localization;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using FinSight.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Website.Models;

public sealed class ProductCostIngredientForm
{
    public string Name { get; set; } = "";
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? PackQuantity { get; set; }
    public IngredientUnit? PackUnit { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? PackPrice { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? UsedQuantity { get; set; }
    public IngredientUnit? UsedUnit { get; set; }
}
public sealed class ProductCostForm
{
    public List<ProductCostIngredientForm> Ingredients { get; set; } = [new()];
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? UsableOutput { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? PackagingPerUnit { get; set; }
    [ModelBinder(BinderType = typeof(WebsiteNumberModelBinder))] public decimal? OtherVariableCostPerUnit { get; set; }
    public ProductCostInputs Inputs()
    {
        if (!UsableOutput.HasValue || Ingredients.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name)
            || !x.PackQuantity.HasValue || !x.PackUnit.HasValue || !x.PackPrice.HasValue || !x.UsedQuantity.HasValue || !x.UsedUnit.HasValue))
            throw new ProductCostValidationException("Complete every ingredient field and the usable batch output. Blank required amounts are not zero.");
        return new(Ingredients.Select(x => new ProductCostIngredient(x.Name, x.PackQuantity!.Value, x.PackUnit!.Value,
            x.PackPrice!.Value, x.UsedQuantity!.Value, x.UsedUnit!.Value)).ToArray(), UsableOutput.Value, PackagingPerUnit, OtherVariableCostPerUnit);
    }
    public static ProductCostForm From(ProductCostInputs inputs) => new()
    {
        UsableOutput = inputs.UsableOutput, PackagingPerUnit = inputs.PackagingPerUnit, OtherVariableCostPerUnit = inputs.OtherVariableCostPerUnit,
        Ingredients = inputs.Ingredients.Select(x => new ProductCostIngredientForm { Name = x.Name, PackQuantity = x.PackQuantity,
            PackUnit = x.PackUnit, PackPrice = x.PackPrice, UsedQuantity = x.UsedQuantity, UsedUnit = x.UsedUnit }).ToList()
    };
}
