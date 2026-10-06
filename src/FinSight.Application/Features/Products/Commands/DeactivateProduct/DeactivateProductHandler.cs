using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using MediatR;

namespace FinSight.Application.Features.Products.Commands.DeactivateProduct;

public sealed class DeactivateProductHandler(
    IProductRepository productRepository,
    ICurrentUserContext currentUserContext)
    : IRequestHandler<DeactivateProductCommand>
{
    public async Task Handle(
        DeactivateProductCommand request,
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

        product.Deactivate();
        await productRepository.SaveChangesAsync(cancellationToken);
    }
}
