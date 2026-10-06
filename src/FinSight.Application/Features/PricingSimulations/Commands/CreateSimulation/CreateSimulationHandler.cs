using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Services;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.CreateSimulation;

public sealed class CreateSimulationHandler(
    IProductRepository productRepository,
    IPricingSimulationRepository simulationRepository,
    ICurrentUserContext currentUserContext,
    PricingSimulationCalculator calculator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSimulationCommand, PricingSimulationResponse>
{
    public async Task<PricingSimulationResponse> Handle(
        CreateSimulationCommand request,
        CancellationToken cancellationToken)
    {
        var product = await productRepository.GetActiveByIdAsync(
            request.ProductId,
            currentUserContext.BusinessId,
            cancellationToken);

        if (product is null)
        {
            throw new NotFoundException(nameof(Product), request.ProductId);
        }

        var simulation = new PricingSimulation(
            Guid.NewGuid(),
            currentUserContext.BusinessId,
            product.Id,
            currentUserContext.UserId,
            product.SellingPrice,
            product.UnitCost,
            request.ProposedPrice,
            request.CurrentSalesQuantity,
            request.ExpectedSalesQuantity,
            timeProvider.GetUtcNow().UtcDateTime,
            calculator);

        await simulationRepository.AddAsync(simulation, cancellationToken);
        await simulationRepository.SaveChangesAsync(cancellationToken);

        return PricingSimulationResponse.FromSimulation(simulation);
    }
}
