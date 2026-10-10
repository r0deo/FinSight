using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.RestoreSimulation;

public sealed class RestoreSimulationHandler(
    IPricingSimulationRepository simulationRepository,
    ICurrentUserContext currentUserContext,
    TimeProvider timeProvider)
    : IRequestHandler<RestoreSimulationCommand, PricingSimulationVersionResponse>
{
    public async Task<PricingSimulationVersionResponse> Handle(
        RestoreSimulationCommand request,
        CancellationToken cancellationToken)
    {
        var simulation = await simulationRepository.GetByIdForUpdateAsync(
            request.SimulationId,
            currentUserContext.BusinessId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(PricingSimulation), request.SimulationId);
        simulation.Restore(timeProvider.GetUtcNow().UtcDateTime, request.Version);
        await simulationRepository.SaveChangesAsync(cancellationToken);
        return new PricingSimulationVersionResponse(simulation.Version);
    }
}
