using MediatR;

namespace FinSight.Application.Features.Products.Commands.ReactivateProduct;

public sealed record ReactivateProductCommand(Guid ProductId) : IRequest;
