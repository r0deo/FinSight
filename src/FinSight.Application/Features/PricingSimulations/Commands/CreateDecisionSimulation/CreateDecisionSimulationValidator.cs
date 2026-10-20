using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Commands.CreateDecisionSimulation;

public sealed class CreateDecisionSimulationValidator : AbstractValidator<CreateDecisionSimulationCommand>
{
    public CreateDecisionSimulationValidator()
    {
        RuleFor(x => x.AdditionalDecisionCost).Must(x => !x.HasValue || x >= 0 && x <= FinSight.Domain.Services.DeliverySimulationCalculator.MaximumMoney && decimal.Round(x.Value, 2) == x)
            .WithMessage("Enter a non-negative amount with at most two decimal places within the supported range.").WithErrorCode("CostContext");
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProposedPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CurrentSalesQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpectedSalesQuantity).GreaterThanOrEqualTo(0).When(x => x.ExpectedSalesQuantity.HasValue);
        RuleFor(x => x.ComparisonPeriod).IsInEnum();
        RuleFor(x => x.DeclinePercentage).InclusiveBetween(0, 100);
        RuleFor(x => x.PreviewCurrentPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PreviewUnitCost).GreaterThanOrEqualTo(0);
    }
}
