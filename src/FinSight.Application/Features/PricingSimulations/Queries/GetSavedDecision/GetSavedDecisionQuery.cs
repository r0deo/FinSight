using FinSight.Domain.Models;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.GetSavedDecision;

public sealed record GetSavedDecisionQuery(Guid SimulationId) : IRequest<SavedDecisionResponse>;

public sealed record SavedDecisionResponse(
    PricingSimulationResponse Simulation,
    PricingDecisionResult Decision,
    string Currency);
