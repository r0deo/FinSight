using FinSight.Application.Common.Interfaces;
using MediatR;

namespace FinSight.Application.Features.Products.Queries.GetProducts;

public sealed class GetProductsHandler(
    IProductRepository productRepository,
    ICurrentUserContext currentUserContext)
    : IRequestHandler<GetProductsQuery, GetProductsResponse>
{
    public async Task<GetProductsResponse> Handle(
        GetProductsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await productRepository.GetPagedAsync(
            currentUserContext.BusinessId,
            request.Page,
            request.PageSize,
            request.IncludeInactive,
            cancellationToken);
        var items = result.Items.Select(ProductResponse.FromProduct).ToArray();
        var totalPages = (int)Math.Ceiling(result.TotalCount / (double)request.PageSize);

        return new GetProductsResponse(
            items,
            request.Page,
            request.PageSize,
            result.TotalCount,
            totalPages);
    }
}
