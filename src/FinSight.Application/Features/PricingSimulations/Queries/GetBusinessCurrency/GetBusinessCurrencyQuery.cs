using FinSight.Application.Common.Interfaces;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Queries.GetBusinessCurrency;

public sealed record GetBusinessCurrencyQuery : IRequest<string>;

public sealed class GetBusinessCurrencyHandler(
    IBusinessCurrencyReader reader, ICurrentUserContext currentUser)
    : IRequestHandler<GetBusinessCurrencyQuery, string>
{
    public async Task<string> Handle(GetBusinessCurrencyQuery request, CancellationToken cancellationToken) =>
        await reader.GetCurrencyAsync(currentUser.BusinessId, cancellationToken)
        ?? throw new InvalidOperationException("The current business is unavailable.");
}
