using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Services;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.UpdateSimulation;

public sealed class UpdateSimulationHandler(
    IPricingSimulationRepository simulationRepository,
    ICurrentUserContext currentUserContext,
    PricingSimulationCalculator calculator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateSimulationCommand>
{
    public async Task Handle(
        UpdateSimulationCommand request,
        CancellationToken cancellationToken)
    {
        var simulation = await simulationRepository.GetByIdForUpdateAsync(
            request.SimulationId,
            currentUserContext.BusinessId,
            cancellationToken);

        if (simulation is null)
        {
            throw new NotFoundException(nameof(PricingSimulation), request.SimulationId);
        }

        simulation.Update(
            request.ProposedPrice,
            request.CurrentSalesQuantity,
            request.ExpectedSalesQuantity,
            timeProvider.GetUtcNow().UtcDateTime,
            calculator);
        await simulationRepository.SaveChangesAsync(cancellationToken);
    }
}
