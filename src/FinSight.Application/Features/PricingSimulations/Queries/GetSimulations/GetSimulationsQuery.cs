using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.GetSimulations;

public sealed record GetSimulationsQuery(
    int Page = 1,
    int PageSize = 20,
    bool IncludeArchived = false)
    : IRequest<GetSimulationsResponse>;
