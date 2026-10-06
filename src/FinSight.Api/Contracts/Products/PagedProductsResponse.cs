namespace FinSight.Api.Contracts.Products;

public sealed record PagedProductsResponse(
    IReadOnlyList<ProductResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
