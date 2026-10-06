using FinSight.Domain.Entities;
using FluentValidation;

namespace FinSight.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .MaximumLength(Product.MaxNameLength);
        RuleFor(command => command.Category)
            .NotEmpty()
            .Must(category => !string.IsNullOrWhiteSpace(category));
        RuleFor(command => command.SellingPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.UnitCost).GreaterThanOrEqualTo(0);
    }
}
