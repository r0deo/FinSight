using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.GetSimulationById;

public sealed record GetSimulationByIdQuery(Guid SimulationId)
    : IRequest<PricingSimulationResponse>;
