using FinSight.Application.Common.Interfaces;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.GetSimulations;

public sealed class GetSimulationsHandler(
    IPricingSimulationRepository simulationRepository,
    ICurrentUserContext currentUserContext)
    : IRequestHandler<GetSimulationsQuery, GetSimulationsResponse>
{
    public async Task<GetSimulationsResponse> Handle(
        GetSimulationsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await simulationRepository.GetPagedAsync(
            currentUserContext.BusinessId,
            request.Page,
            request.PageSize,
            request.IncludeArchived,
            cancellationToken);
        var items = result.Items
            .Select(PricingSimulationResponse.FromSimulation)
            .ToArray();
        var totalPages = (int)Math.Ceiling(result.TotalCount / (double)request.PageSize);

        return new GetSimulationsResponse(
            items,
            request.Page,
            request.PageSize,
            result.TotalCount,
            totalPages);
    }
}
