using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingSimulation;

public sealed record PreviewPricingSimulationQuery(
    Guid ProductId,
    decimal ProposedPrice,
    int CurrentSalesQuantity,
    int ExpectedSalesQuantity)
    : IRequest<PreviewPricingSimulationResponse>;
