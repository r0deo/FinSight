namespace FinSight.Api.Contracts.Products;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Category,
    string? Size,
    decimal SellingPrice,
    decimal UnitCost,
    bool IsActive);
