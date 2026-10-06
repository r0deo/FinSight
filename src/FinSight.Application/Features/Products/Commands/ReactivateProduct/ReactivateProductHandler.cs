using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using MediatR;

namespace FinSight.Application.Features.Products.Commands.ReactivateProduct;

public sealed class ReactivateProductHandler(
    IProductRepository productRepository,
    ICurrentUserContext currentUserContext)
    : IRequestHandler<ReactivateProductCommand>
{
    public async Task Handle(
        ReactivateProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdForUpdateAsync(
            request.ProductId,
            currentUserContext.BusinessId,
            cancellationToken);

        if (product is null)
        {
            throw new NotFoundException(nameof(Product), request.ProductId);
        }

        product.Reactivate();
        await productRepository.SaveChangesAsync(cancellationToken);
    }
}
