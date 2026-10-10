using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.RestoreSimulation;

public sealed record RestoreSimulationCommand(Guid SimulationId, long Version)
    : IRequest<PricingSimulationVersionResponse>;
