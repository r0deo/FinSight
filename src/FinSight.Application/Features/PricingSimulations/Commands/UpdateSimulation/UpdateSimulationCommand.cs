using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.UpdateSimulation;

public sealed record UpdateSimulationCommand(
    Guid SimulationId,
    decimal ProposedPrice,
    int CurrentSalesQuantity,
    int ExpectedSalesQuantity,
    long Version) : IRequest<PricingSimulationVersionResponse>;
