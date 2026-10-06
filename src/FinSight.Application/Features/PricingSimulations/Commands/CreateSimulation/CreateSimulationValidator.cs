using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Commands.CreateSimulation;

public sealed class CreateSimulationValidator : AbstractValidator<CreateSimulationCommand>
{
    public CreateSimulationValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
        RuleFor(command => command.ProposedPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.CurrentSalesQuantity).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ExpectedSalesQuantity).GreaterThanOrEqualTo(0);
    }
}
