using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Commands.ArchiveSimulation;

public sealed class ArchiveSimulationValidator : AbstractValidator<ArchiveSimulationCommand>
{
    public ArchiveSimulationValidator()
    {
        RuleFor(command => command.SimulationId).NotEmpty();
        RuleFor(command => command.Version).GreaterThan(0);
    }
}
