using FluentValidation;

namespace FinSight.Application.Features.Products.Commands.DeactivateProduct;

public sealed class DeactivateProductValidator : AbstractValidator<DeactivateProductCommand>
{
    public DeactivateProductValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
    }
}
