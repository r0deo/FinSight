using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using MediatR;

namespace FinSight.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductHandler(
    IProductRepository productRepository,
    ICurrentUserContext currentUserContext)
    : IRequestHandler<CreateProductCommand, CreateProductResponse>
{
    public async Task<CreateProductResponse> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        ProductCostSelection.Validate(request.CostInputs, request.UnitCost);
        var product = new Product(
            Guid.NewGuid(),
            currentUserContext.BusinessId,
            request.Name,
            request.Category,
            request.Size,
            request.SellingPrice,
            request.UnitCost);

        if (request.CostInputs is not null) product.ApplyCostBreakdown(request.CostInputs);
        await productRepository.AddAsync(product, cancellationToken);
        await productRepository.SaveChangesAsync(cancellationToken);

        return new CreateProductResponse(
            product.Id,
            product.Name,
            product.Category,
            product.Size,
            product.SellingPrice,
            product.UnitCost,
            product.IsActive);
    }
}
