using FinSight.Domain.Entities;
using FinSight.Application.Common.Models;

namespace FinSight.Application.Common.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(
        Guid productId,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<Product?> GetByIdForUpdateAsync(
        Guid productId,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<Product?> GetActiveByIdAsync(
        Guid productId,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Product>> GetPagedAsync(
        Guid businessId,
        int page,
        int pageSize,
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
