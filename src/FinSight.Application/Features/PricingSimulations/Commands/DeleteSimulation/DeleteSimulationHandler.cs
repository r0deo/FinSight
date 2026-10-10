using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.DeleteSimulation;

public sealed class DeleteSimulationHandler(
    IPricingSimulationRepository simulationRepository,
    ICurrentUserContext currentUserContext,
    TimeProvider timeProvider)
    : IRequestHandler<DeleteSimulationCommand, PricingSimulationVersionResponse>
{
    public async Task<PricingSimulationVersionResponse> Handle(
        DeleteSimulationCommand request,
        CancellationToken cancellationToken)
    {
        var simulation = await simulationRepository.GetByIdForUpdateAsync(
            request.SimulationId,
            currentUserContext.BusinessId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(PricingSimulation), request.SimulationId);
        simulation.Delete(timeProvider.GetUtcNow().UtcDateTime, request.Version);
        await simulationRepository.SaveChangesAsync(cancellationToken);
        return new PricingSimulationVersionResponse(simulation.Version);
    }
}
