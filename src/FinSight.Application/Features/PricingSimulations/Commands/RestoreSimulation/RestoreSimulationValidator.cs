using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Commands.RestoreSimulation;

public sealed class RestoreSimulationValidator : AbstractValidator<RestoreSimulationCommand>
{
    public RestoreSimulationValidator()
    {
        RuleFor(command => command.SimulationId).NotEmpty();
        RuleFor(command => command.Version).GreaterThan(0);
    }
}
