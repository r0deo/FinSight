namespace FinSight.Api.Contracts.Products;

public sealed record UpdateProductRequest(
    string Name,
    string Category,
    string? Size,
    decimal SellingPrice,
    decimal UnitCost);
