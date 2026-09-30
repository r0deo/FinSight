using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(FinSightDbContext dbContext)
    : IProductRepository
{
    public Task<Product?> GetByIdAsync(
        Guid productId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(
                product => product.Id == productId
                    && product.BusinessId == businessId,
                cancellationToken);
    }
}
