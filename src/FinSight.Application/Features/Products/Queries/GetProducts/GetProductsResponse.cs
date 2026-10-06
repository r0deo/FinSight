namespace FinSight.Application.Features.Products.Queries.GetProducts;

public sealed record GetProductsResponse(
    IReadOnlyList<ProductResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
