using FluentValidation;

namespace FinSight.Application.Features.Products.Queries.GetProductById;

public sealed class GetProductByIdValidator : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdValidator()
    {
        RuleFor(query => query.ProductId).NotEmpty();
    }
}
