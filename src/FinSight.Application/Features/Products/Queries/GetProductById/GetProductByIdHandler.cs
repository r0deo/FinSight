using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using MediatR;

namespace FinSight.Application.Features.Products.Queries.GetProductById;

public sealed class GetProductByIdHandler(
    IProductRepository productRepository,
    ICurrentUserContext currentUserContext)
    : IRequestHandler<GetProductByIdQuery, ProductResponse>
{
    public async Task<ProductResponse> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(
            request.ProductId,
            currentUserContext.BusinessId,
            cancellationToken);

        if (product is null)
        {
            throw new NotFoundException(nameof(Product), request.ProductId);
        }

        return ProductResponse.FromProduct(product);
    }
}
