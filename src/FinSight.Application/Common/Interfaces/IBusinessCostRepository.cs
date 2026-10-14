using FinSight.Domain.Entities;
namespace FinSight.Application.Common.Interfaces;

public interface IBusinessCostRepository
{
    Task<IReadOnlyList<BusinessCost>> GetMonthAsync(Guid businessId, DateOnly month, CancellationToken ct);
    Task<BusinessCost?> GetAsync(Guid id, Guid businessId, bool tracked, CancellationToken ct);
    Task AddAsync(BusinessCost cost, CancellationToken ct);
    void Remove(BusinessCost cost);
    Task SaveChangesAsync(CancellationToken ct);
}
