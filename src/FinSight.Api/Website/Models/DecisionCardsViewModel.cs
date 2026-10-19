using FinSight.Application.Features.Products.Queries.GetProducts;

namespace FinSight.Api.Website.Models;

public sealed record DecisionCardsViewModel(string Selected, Guid? ProductId = null)
{
    public static string Route(string decision) => decision switch
    {
        "discount" => "/simulations/discount", "delivery" => "/simulations/delivery", _ => "/simulations/preview"
    };
}
public sealed record DecisionProductPickerViewModel(string Decision, GetProductsResponse Products);
