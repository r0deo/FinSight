using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.ArchiveSimulation;

public sealed class ArchiveSimulationHandler(
    IPricingSimulationRepository simulationRepository,
    ICurrentUserContext currentUserContext,
    TimeProvider timeProvider)
    : IRequestHandler<ArchiveSimulationCommand, PricingSimulationVersionResponse>
{
    public async Task<PricingSimulationVersionResponse> Handle(
        ArchiveSimulationCommand request,
        CancellationToken cancellationToken)
    {
        var simulation = await simulationRepository.GetByIdForUpdateAsync(
            request.SimulationId,
            currentUserContext.BusinessId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(PricingSimulation), request.SimulationId);
        simulation.Archive(timeProvider.GetUtcNow().UtcDateTime, request.Version);
        await simulationRepository.SaveChangesAsync(cancellationToken);
        return new PricingSimulationVersionResponse(simulation.Version);
    }
}
