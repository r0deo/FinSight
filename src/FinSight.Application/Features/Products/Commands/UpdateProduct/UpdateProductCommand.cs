using MediatR;

namespace FinSight.Application.Features.Products.Commands.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid ProductId,
    string Name,
    string Category,
    string? Size,
    decimal SellingPrice,
    decimal UnitCost) : IRequest;
