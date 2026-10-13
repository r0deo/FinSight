using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class WhatIfSimulationRepository(FinSightDbContext db) : IWhatIfSimulationRepository
{
    public async Task AddAsync(WhatIfSimulation simulation, CancellationToken cancellationToken) =>
        await db.WhatIfSimulations.AddAsync(simulation, cancellationToken);

    public Task<WhatIfSimulation?> GetByIdAsync(Guid id, Guid businessId,
        CancellationToken cancellationToken) => db.WhatIfSimulations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId, cancellationToken);

    public async Task<PagedResult<WhatIfSimulation>> GetPagedAsync(Guid businessId, int page,
        int pageSize, CancellationToken cancellationToken)
    {
        var query = db.WhatIfSimulations.AsNoTracking().Where(x => x.BusinessId == businessId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<WhatIfSimulation>(items, total);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
