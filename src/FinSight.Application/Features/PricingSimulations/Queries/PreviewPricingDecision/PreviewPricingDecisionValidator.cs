using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingDecision;

public sealed class PreviewPricingDecisionValidator : AbstractValidator<PreviewPricingDecisionQuery>
{
    public PreviewPricingDecisionValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProposedPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CurrentSalesQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpectedSalesQuantity).GreaterThanOrEqualTo(0).When(x => x.ExpectedSalesQuantity.HasValue);
        RuleFor(x => x.DeclinePercentage).InclusiveBetween(0, 100);
    }
}
