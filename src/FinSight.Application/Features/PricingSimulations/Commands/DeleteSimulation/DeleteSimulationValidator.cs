using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Commands.DeleteSimulation;

public sealed class DeleteSimulationValidator : AbstractValidator<DeleteSimulationCommand>
{
    public DeleteSimulationValidator()
    {
        RuleFor(command => command.SimulationId).NotEmpty();
        RuleFor(command => command.Version).GreaterThan(0);
    }
}
