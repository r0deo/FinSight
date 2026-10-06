namespace FinSight.Api.Contracts.PricingSimulations;

public sealed record PagedPricingSimulationsResponse(
    IReadOnlyList<PricingSimulationResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
