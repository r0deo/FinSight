using FinSight.Domain.Entities;
using FluentValidation;

namespace FinSight.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
        RuleFor(command => command.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .MaximumLength(Product.MaxNameLength);
        RuleFor(command => command.Category)
            .NotEmpty()
            .Must(category => !string.IsNullOrWhiteSpace(category));
        RuleFor(command => command.SellingPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.UnitCost).GreaterThanOrEqualTo(0);
        When(x => x.CostInputs is not null, () => RuleFor(x => x.CostInputs!).SetValidator(new ProductCostInputsValidator()));
    }
}
