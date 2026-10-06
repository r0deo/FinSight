using FluentValidation;

namespace FinSight.Application.Features.PricingSimulations.Queries.GetSimulations;

public sealed class GetSimulationsValidator : AbstractValidator<GetSimulationsQuery>
{
    public GetSimulationsValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
