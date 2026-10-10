using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Commands.UpdateSimulation;

public sealed class UpdateSimulationValidator : AbstractValidator<UpdateSimulationCommand>
{
    public UpdateSimulationValidator()
    {
        RuleFor(command => command.SimulationId).NotEmpty();
        RuleFor(command => command.ProposedPrice).GreaterThanOrEqualTo(0);
        RuleFor(command => command.CurrentSalesQuantity).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ExpectedSalesQuantity).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Version).GreaterThan(0);
    }
}
