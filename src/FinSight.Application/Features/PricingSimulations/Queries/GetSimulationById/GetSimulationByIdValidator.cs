using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Queries.GetSimulationById;

public sealed class GetSimulationByIdValidator : AbstractValidator<GetSimulationByIdQuery>
{
    public GetSimulationByIdValidator()
    {
        RuleFor(query => query.SimulationId).NotEmpty();
    }
}
