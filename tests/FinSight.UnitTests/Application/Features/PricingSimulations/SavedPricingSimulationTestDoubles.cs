using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Domain.Entities;

namespace FinSight.UnitTests.Application.Features.PricingSimulations;

internal sealed class StubPricingSimulationRepository : IPricingSimulationRepository
{
    public PricingSimulation? ReadSimulation { get; set; }

    public PricingSimulation? TrackedSimulation { get; set; }

    public PagedResult<PricingSimulation> PagedResult { get; set; } = new([], 0);

    public PricingSimulation? AddedSimulation { get; private set; }

    public Guid? RequestedSimulationId { get; private set; }

    public Guid? RequestedBusinessId { get; private set; }

    public int SaveChangesCount { get; private set; }

    public Task AddAsync(
        PricingSimulation simulation,
        CancellationToken cancellationToken = default)
    {
        AddedSimulation = simulation;
        return Task.CompletedTask;
    }

    public Task<PricingSimulation?> GetByIdAsync(
        Guid simulationId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        RequestedSimulationId = simulationId;
        RequestedBusinessId = businessId;
        return Task.FromResult(ReadSimulation);
    }

    public Task<PricingSimulation?> GetByIdForUpdateAsync(
        Guid simulationId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        RequestedSimulationId = simulationId;
        RequestedBusinessId = businessId;
        return Task.FromResult(TrackedSimulation);
    }

    public Task<PagedResult<PricingSimulation>> GetPagedAsync(
        Guid businessId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        RequestedBusinessId = businessId;
        return Task.FromResult(PagedResult);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
