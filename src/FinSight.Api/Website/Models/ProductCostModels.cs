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
        var output = Required(UsableOutput, "Enter the usable batch output.", nameof(UsableOutput));
        return new(Ingredients.Select((x, i) => new ProductCostIngredient(x.Name,
            Required(x.PackQuantity, "Enter this quantity.", $"Ingredients[{i}].PackQuantity"),
            Required(x.PackUnit, "Choose a supported ingredient unit.", $"Ingredients[{i}].PackUnit"),
            Required(x.PackPrice, "Enter the purchase-pack price.", $"Ingredients[{i}].PackPrice"),
            Required(x.UsedQuantity, "Enter this quantity.", $"Ingredients[{i}].UsedQuantity"),
            Required(x.UsedUnit, "Choose a supported ingredient unit.", $"Ingredients[{i}].UsedUnit"))).ToArray(),
            output, PackagingPerUnit, OtherVariableCostPerUnit);
    }
    private static T Required<T>(T? value, string message, string path) where T : struct =>
        value ?? throw new ProductCostValidationException(message, path);
    public static ProductCostForm From(ProductCostInputs inputs) => new()
    {
        UsableOutput = inputs.UsableOutput, PackagingPerUnit = inputs.PackagingPerUnit, OtherVariableCostPerUnit = inputs.OtherVariableCostPerUnit,
        Ingredients = inputs.Ingredients.Select(x => new ProductCostIngredientForm { Name = x.Name, PackQuantity = x.PackQuantity,
            PackUnit = x.PackUnit, PackPrice = x.PackPrice, UsedQuantity = x.UsedQuantity, UsedUnit = x.UsedUnit }).ToList()
    };
}
