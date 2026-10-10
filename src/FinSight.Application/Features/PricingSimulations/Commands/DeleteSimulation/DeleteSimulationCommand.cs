using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.DeleteSimulation;

public sealed record DeleteSimulationCommand(Guid SimulationId, long Version)
    : IRequest<PricingSimulationVersionResponse>;
