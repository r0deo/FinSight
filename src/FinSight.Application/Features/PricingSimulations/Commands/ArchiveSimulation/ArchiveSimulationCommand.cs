using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.ArchiveSimulation;

public sealed record ArchiveSimulationCommand(Guid SimulationId, long Version)
    : IRequest<PricingSimulationVersionResponse>;
