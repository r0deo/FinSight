using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Commands.CreateSimulation;

public sealed record CreateSimulationCommand(
    Guid ProductId,
    decimal ProposedPrice,
    int CurrentSalesQuantity,
    int ExpectedSalesQuantity) : IRequest<PricingSimulationResponse>;
