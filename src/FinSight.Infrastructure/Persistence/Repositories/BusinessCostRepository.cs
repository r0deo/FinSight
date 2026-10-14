using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;
namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class BusinessCostRepository(FinSightDbContext db) : IBusinessCostRepository
{
    public async Task<(IReadOnlyList<BusinessCost> Entries, BusinessCostMonth? Review)> GetMonthSnapshotAsync(Guid businessId, DateOnly month, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var entries = await GetMonthAsync(businessId, month, ct);
        var review = await db.BusinessCostMonths.AsNoTracking().SingleOrDefaultAsync(x => x.BusinessId == businessId && x.Month == month, ct);
        await transaction.CommitAsync(ct);
        return (entries, review);
    }
    public Task<BusinessCostMonth?> GetTrackedMonthAsync(Guid businessId, DateOnly month, CancellationToken ct) =>
        db.BusinessCostMonths.SingleOrDefaultAsync(x => x.BusinessId == businessId && x.Month == month, ct);
    public async Task AddMonthAsync(BusinessCostMonth month, CancellationToken ct) => await db.BusinessCostMonths.AddAsync(month, ct);
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
        // Any edit invalidates the declaration, including both months when moving an entry.
        // Month version and cost changes are saved atomically by EF's transaction.
        var affected = db.ChangeTracker.Entries<BusinessCost>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .SelectMany(e => new[] { (e.Entity.BusinessId, e.Entity.Month),
                (e.Entity.BusinessId, e.State == EntityState.Added ? e.Entity.Month : e.OriginalValues.GetValue<DateOnly>(nameof(BusinessCost.Month))) })
            .Distinct().OrderBy(x => x.BusinessId).ThenBy(x => x.Item2).ToArray();
        foreach (var (businessId, month) in affected)
        {
            var review = await GetTrackedMonthAsync(businessId, month, ct);
            if (review is null) await AddMonthAsync(new BusinessCostMonth(businessId, month), ct);
            else review.Reopen();
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException(); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_BusinessCostMonths" })
        { throw new ConcurrencyConflictException(); }
    }
}
