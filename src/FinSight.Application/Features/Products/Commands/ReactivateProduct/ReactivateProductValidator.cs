using FluentValidation;

namespace FinSight.Application.Features.Products.Commands.ReactivateProduct;

public sealed class ReactivateProductValidator : AbstractValidator<ReactivateProductCommand>
{
    public ReactivateProductValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
    }
}
