using System.Security.Cryptography;
using System.Text.Json;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using FinSight.Domain.Services;
using FinSight.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace FinSight.Application.Features.BusinessCosts;

public abstract record CostLinkedRequest
{
    public CostLinkOptions? CostLink { get; init; }
    public string? CostBasis { get; init; }
}

public sealed record PreviewCostContextQuery(CostLinkOptions Settings, SalesPeriod Period,
    decimal? Current, IReadOnlyList<ContributionOption> Options) : IRequest<SimulationCostContext>;
public sealed class PreviewCostContextHandler(SimulationCostContextService service)
    : IRequestHandler<PreviewCostContextQuery, SimulationCostContext>
{
    public Task<SimulationCostContext> Handle(PreviewCostContextQuery query, CancellationToken ct) =>
        SimulationCostContextService.Capture(service, query.Settings, query.Period, query.Current, query.Options, ct);
}

public sealed class SimulationCostContextService(IBusinessCostRepository costs, ICurrentUserContext user)
{
    private readonly IBusinessCostRepository costs = costs;
    private readonly ICurrentUserContext user = user;
    public static async Task<SimulationCostContext> Capture(SimulationCostContextService? service, CostLinkOptions? input,
        SalesPeriod period, decimal? current, IReadOnlyList<ContributionOption> options, CancellationToken ct,
        string? expectedBasis = null, bool saving = false)
    {
        try
        {
            var settings = SimulationCostCalculator.Validate(input);
            IReadOnlyList<LinkedCostEntry> entries = []; var reviewed = false;
            long? reviewVersion = null;
            if (settings.Enabled)
            {
                if (service is null) throw new InvalidOperationException("Cost context service is required.");
                var month = await service.costs.GetMonthSnapshotAsync(service.user.BusinessId, settings.Month!.Value, ct);
                entries = month.Entries.OrderBy(x => x.Id).Select(x => new LinkedCostEntry(x.Id, x.Name, x.Category,
                    x.Amount, x.Certainty, x.Inclusion, x.Version, false)).ToArray();
                reviewed = month.Review?.ReviewedAtUtc is not null; reviewVersion = month.Review?.Version;
            }
            var result = SimulationCostCalculator.Calculate(settings, period, entries, reviewed, current, options);
            var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { Business = settings.Enabled ? service!.user.BusinessId : Guid.Empty, ReviewVersion = reviewVersion, Snapshot = result })));
            // Legacy callers can still save disabled contexts without a proof. Linking always requires
            // the exact server-calculated basis shown in the preview, including settings and financial rows.
            if (saving && (settings.Enabled || !string.IsNullOrEmpty(expectedBasis)) && expectedBasis != fingerprint)
                throw new PricingSimulationConflictException("Costs or assumptions changed. Calculate again before saving.");
            return result with { BasisFingerprint = fingerprint };
        }
        catch (CostContextValidationException error)
        {
            throw new ValidationException([new ValidationFailure(error.Field, error.Message) { ErrorCode = "CostContext" }]);
        }
    }
}
