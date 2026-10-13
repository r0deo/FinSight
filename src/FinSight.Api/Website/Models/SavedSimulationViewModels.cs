using FinSight.Application.Features.PricingSimulations;
using FinSight.Application.Features.PricingSimulations.Queries.GetSimulations;
using FinSight.Domain.Models;

namespace FinSight.Api.Website.Models;

public sealed record SavedSimulationListViewModel(
    GetSimulationsResponse Page,
    bool IncludeArchived);

public sealed record SavedSimulationDetailViewModel(
    PricingSimulationResponse Simulation,
    PricingDecisionResult Decision,
    string Currency,
    string ProductName);
