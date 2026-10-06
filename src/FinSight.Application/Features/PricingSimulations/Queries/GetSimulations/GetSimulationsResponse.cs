namespace FinSight.Application.Features.PricingSimulations.Queries.GetSimulations;

public sealed record GetSimulationsResponse(
    IReadOnlyList<PricingSimulationResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
