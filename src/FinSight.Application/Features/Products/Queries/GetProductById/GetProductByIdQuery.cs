using MediatR;

namespace FinSight.Application.Features.Products.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid ProductId) : IRequest<ProductResponse>;
