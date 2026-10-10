using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Application.Common.Exceptions;
using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class PricingSimulationRepository(FinSightDbContext dbContext)
    : IPricingSimulationRepository
{
    public async Task AddAsync(
        PricingSimulation simulation,
        CancellationToken cancellationToken = default)
    {
        await dbContext.PricingSimulations.AddAsync(simulation, cancellationToken);
    }

    public Task<PricingSimulation?> GetByIdAsync(
        Guid simulationId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PricingSimulations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                simulation => simulation.Id == simulationId
                    && simulation.BusinessId == businessId
                    && simulation.DeletedAtUtc == null,
                cancellationToken);
    }

    public Task<PricingSimulation?> GetByIdForUpdateAsync(
        Guid simulationId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.PricingSimulations.SingleOrDefaultAsync(
            simulation => simulation.Id == simulationId
                && simulation.BusinessId == businessId,
            cancellationToken);
    }

    public async Task<PagedResult<PricingSimulation>> GetPagedAsync(
        Guid businessId,
        int page,
        int pageSize,
        bool includeArchived,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.PricingSimulations
            .AsNoTracking()
            .Where(simulation => simulation.BusinessId == businessId
                && simulation.DeletedAtUtc == null);

        if (!includeArchived)
        {
            query = query.Where(simulation => simulation.ArchivedAtUtc == null);
        }
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(simulation => simulation.CreatedAtUtc)
            .ThenByDescending(simulation => simulation.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PricingSimulation>(items, totalCount);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
    }
}
