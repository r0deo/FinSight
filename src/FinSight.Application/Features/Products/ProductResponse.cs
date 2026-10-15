using FinSight.Domain.Entities;

namespace FinSight.Application.Features.Products;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Category,
    string? Size,
    decimal SellingPrice,
    decimal UnitCost,
    bool IsActive)
{
    public FinSight.Domain.Models.ProductCostBreakdown? CostBreakdown { get; init; }
    public static ProductResponse FromProduct(Product product) => new(
        product.Id,
        product.Name,
        product.Category,
        product.Size,
        product.SellingPrice,
        product.UnitCost,
        product.IsActive) { CostBreakdown = product.CostBreakdown };
}
