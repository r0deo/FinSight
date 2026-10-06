using FinSight.Application.Common.Models;
using FinSight.Domain.Entities;

namespace FinSight.Application.Common.Interfaces;

public interface IPricingSimulationRepository
{
    Task AddAsync(
        PricingSimulation simulation,
        CancellationToken cancellationToken = default);

    Task<PricingSimulation?> GetByIdAsync(
        Guid simulationId,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<PricingSimulation?> GetByIdForUpdateAsync(
        Guid simulationId,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<PricingSimulation>> GetPagedAsync(
        Guid businessId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
