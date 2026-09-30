namespace FinSight.Api.Contracts.Products;

public sealed record CreateProductRequest(
    string Name,
    string Category,
    string? Size,
    decimal SellingPrice,
    decimal UnitCost);
