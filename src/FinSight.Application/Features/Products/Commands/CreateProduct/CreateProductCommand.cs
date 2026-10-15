using MediatR;

namespace FinSight.Application.Features.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Category,
    string? Size,
    decimal SellingPrice,
    decimal UnitCost) : IRequest<CreateProductResponse>
{
    public FinSight.Domain.Models.ProductCostInputs? CostInputs { get; init; }
}
