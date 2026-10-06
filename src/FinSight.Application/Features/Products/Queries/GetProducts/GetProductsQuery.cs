using MediatR;

namespace FinSight.Application.Features.Products.Queries.GetProducts;

public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 20,
    bool IncludeInactive = false) : IRequest<GetProductsResponse>;
