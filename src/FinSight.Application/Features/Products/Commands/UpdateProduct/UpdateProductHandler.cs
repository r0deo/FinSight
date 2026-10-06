using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using MediatR;

namespace FinSight.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductHandler(
    IProductRepository productRepository,
    ICurrentUserContext currentUserContext)
    : IRequestHandler<UpdateProductCommand>
{
    public async Task Handle(
        UpdateProductCommand request,
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

        product.UpdateDetails(request.Name, request.Category, request.Size);
        product.UpdateFinancials(request.SellingPrice, request.UnitCost);
        await productRepository.SaveChangesAsync(cancellationToken);
    }
}
