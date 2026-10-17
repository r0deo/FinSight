using FinSight.Domain.Models;
using FinSight.Domain.Services;
using FinSight.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace FinSight.Application.Features.Products;

public sealed record PreviewProductCostQuery(ProductCostInputs Inputs) : IRequest<ProductCostBreakdown>;
public sealed class ProductCostInputsValidator : AbstractValidator<ProductCostInputs>
{
    public ProductCostInputsValidator() => RuleFor(x => x).Custom((inputs, context) =>
    {
        try { new ProductCostCalculator().Calculate(inputs); }
        catch (ProductCostValidationException e) { context.AddFailure(e.FieldPath ?? string.Empty, e.Message); }
    });
}
public sealed class PreviewProductCostValidator : AbstractValidator<PreviewProductCostQuery>
{
    public PreviewProductCostValidator() => RuleFor(x => x.Inputs).NotNull().SetValidator(new ProductCostInputsValidator());
}
public sealed class PreviewProductCostHandler : IRequestHandler<PreviewProductCostQuery, ProductCostBreakdown>
{
    public Task<ProductCostBreakdown> Handle(PreviewProductCostQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProductCostCalculator().Calculate(request.Inputs));
    }
}
public static class ProductCostSelection
{
    public static void Validate(ProductCostInputs? inputs, decimal selectedUnitCost)
    {
        if (inputs is not null && new ProductCostCalculator().Calculate(inputs).Result.EstimatedUnitCost != selectedUnitCost)
            throw new ValidationException("Recalculate and choose Use this unit cost, or keep a manual cost.");
    }
}
