using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Queries.GetSavedDecision;

public sealed class GetSavedDecisionValidator : AbstractValidator<GetSavedDecisionQuery>
{
    public GetSavedDecisionValidator() => RuleFor(x => x.SimulationId).NotEmpty();
}
