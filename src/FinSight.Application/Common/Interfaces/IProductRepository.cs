using FinSight.Domain.Entities;

namespace FinSight.Application.Common.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(
        Guid productId,
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default);
}
