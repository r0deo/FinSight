namespace FinSight.Application.Features.Products.Commands.CreateProduct;

public sealed record CreateProductResponse(
    Guid Id,
    string Name,
    string Category,
    string? Size,
    decimal SellingPrice,
    decimal UnitCost,
    bool IsActive);
