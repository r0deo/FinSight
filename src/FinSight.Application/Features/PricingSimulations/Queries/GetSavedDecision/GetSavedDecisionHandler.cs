using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Services;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.GetSavedDecision;

public sealed class GetSavedDecisionHandler(
    IPricingSimulationRepository simulations,
    IBusinessCurrencyReader currencies,
    ICurrentUserContext currentUser,
    PricingDecisionCalculator calculator)
    : IRequestHandler<GetSavedDecisionQuery, SavedDecisionResponse>
{
    public async Task<SavedDecisionResponse> Handle(GetSavedDecisionQuery request,
        CancellationToken cancellationToken)
    {
        var simulation = await simulations.GetByIdAsync(request.SimulationId,
            currentUser.BusinessId, cancellationToken)
            ?? throw new NotFoundException(nameof(PricingSimulation), request.SimulationId);
        var decision = calculator.Calculate(simulation.CurrentPrice, simulation.ProposedPrice,
            simulation.UnitCost, simulation.CurrentSalesQuantity,
            simulation.ExpectedSalesQuantity, simulation.DeclinePercentage);
        // Stored totals remain authoritative; never consult current product financials.
        var scenarios = decision.Scenarios.Select(scenario =>
            scenario.Kind == PricingScenarioKind.ExpectedSales
                && simulation.ProposedContribution.HasValue && simulation.Difference.HasValue
                ? scenario with { Contribution = simulation.ProposedContribution.Value,
                    Difference = simulation.Difference.Value }
                : scenario).ToArray();
        decision = decision with
        {
            CurrentContribution = simulation.CurrentContribution,
            Scenarios = scenarios,
            IsProposedPriceBelowCost = simulation.IsProposedPriceBelowCost
        };
        var currency = simulation.CurrencySnapshot
            ?? await currencies.GetCurrencyAsync(currentUser.BusinessId, cancellationToken)
            ?? throw new InvalidOperationException("The current business is unavailable.");
        return new SavedDecisionResponse(PricingSimulationResponse.FromSimulation(simulation),
            decision, currency);
    }
}
