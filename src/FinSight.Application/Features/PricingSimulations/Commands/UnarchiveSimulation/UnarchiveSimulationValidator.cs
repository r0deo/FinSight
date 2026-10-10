using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Commands.UnarchiveSimulation;

public sealed class UnarchiveSimulationValidator
    : AbstractValidator<UnarchiveSimulationCommand>
{
    public UnarchiveSimulationValidator()
    {
        RuleFor(command => command.SimulationId).NotEmpty();
        RuleFor(command => command.Version).GreaterThan(0);
    }
}
