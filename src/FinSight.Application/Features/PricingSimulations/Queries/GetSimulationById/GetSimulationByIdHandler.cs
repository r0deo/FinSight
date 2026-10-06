using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.GetSimulationById;

public sealed class GetSimulationByIdHandler(
    IPricingSimulationRepository simulationRepository,
    ICurrentUserContext currentUserContext)
    : IRequestHandler<GetSimulationByIdQuery, PricingSimulationResponse>
{
    public async Task<PricingSimulationResponse> Handle(
        GetSimulationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var simulation = await simulationRepository.GetByIdAsync(
            request.SimulationId,
            currentUserContext.BusinessId,
            cancellationToken);

        if (simulation is null)
        {
            throw new NotFoundException(nameof(PricingSimulation), request.SimulationId);
        }

        return PricingSimulationResponse.FromSimulation(simulation);
    }
}
