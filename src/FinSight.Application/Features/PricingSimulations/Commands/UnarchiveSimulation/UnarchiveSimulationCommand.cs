using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.UnarchiveSimulation;

public sealed record UnarchiveSimulationCommand(Guid SimulationId, long Version)
    : IRequest<PricingSimulationVersionResponse>;
