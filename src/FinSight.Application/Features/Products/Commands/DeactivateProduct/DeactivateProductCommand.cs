using MediatR;

namespace FinSight.Application.Features.Products.Commands.DeactivateProduct;

public sealed record DeactivateProductCommand(Guid ProductId) : IRequest;
