using FinSight.Application.Common.Models;
using FinSight.Domain.Entities;

namespace FinSight.Application.Common.Interfaces;

public interface IWhatIfSimulationRepository
{
    Task AddAsync(WhatIfSimulation simulation, CancellationToken cancellationToken);
    Task<WhatIfSimulation?> GetByIdAsync(Guid id, Guid businessId, CancellationToken cancellationToken);
    Task<PagedResult<WhatIfSimulation>> GetPagedAsync(Guid businessId, int page,
        int pageSize, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
