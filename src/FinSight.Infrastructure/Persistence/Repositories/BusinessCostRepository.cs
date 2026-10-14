using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class BusinessCostRepository(FinSightDbContext db) : IBusinessCostRepository
{
    public async Task<IReadOnlyList<BusinessCost>> GetMonthAsync(Guid businessId, DateOnly month, CancellationToken ct) =>
        await db.BusinessCosts.AsNoTracking().Where(x => x.BusinessId == businessId && x.Month == month)
            .OrderBy(x => x.Category).ThenBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
    public Task<BusinessCost?> GetAsync(Guid id, Guid businessId, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.BusinessCosts : db.BusinessCosts.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.Id == id && x.BusinessId == businessId, ct);
    }
    public async Task AddAsync(BusinessCost cost, CancellationToken ct) => await db.BusinessCosts.AddAsync(cost, ct);
    public void Remove(BusinessCost cost) => db.BusinessCosts.Remove(cost);
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException(); }
    }
}
