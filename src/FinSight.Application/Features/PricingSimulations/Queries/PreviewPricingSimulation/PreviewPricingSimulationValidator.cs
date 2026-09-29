using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingSimulation;

public sealed class PreviewPricingSimulationValidator
    : AbstractValidator<PreviewPricingSimulationQuery>
{
    public PreviewPricingSimulationValidator()
    {
        RuleFor(query => query.ProductId)
            .NotEmpty();

        RuleFor(query => query.ProposedPrice)
            .GreaterThanOrEqualTo(0);

        RuleFor(query => query.CurrentSalesQuantity)
            .GreaterThanOrEqualTo(0);

        RuleFor(query => query.ExpectedSalesQuantity)
            .GreaterThanOrEqualTo(0);
    }
}
