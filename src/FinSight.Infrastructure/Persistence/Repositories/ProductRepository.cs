using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using FinSight.Application.Common.Exceptions;

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

    public Task<Product?> GetByIdForUpdateAsync(
        Guid productId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Products.SingleOrDefaultAsync(
            product => product.Id == productId
                && product.BusinessId == businessId,
            cancellationToken);
    }

    public Task<Product?> GetActiveByIdAsync(
        Guid productId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(
                product => product.Id == productId
                    && product.BusinessId == businessId
                    && product.IsActive,
                cancellationToken);
    }

    public async Task<PagedResult<Product>> GetPagedAsync(
        Guid businessId,
        int page,
        int pageSize,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Products
            .AsNoTracking()
            .Where(product => product.BusinessId == businessId);

        if (!includeInactive)
        {
            query = query.Where(product => product.IsActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(product => product.Name)
            .ThenBy(product => product.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, totalCount);
    }

    public async Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Products.AddAsync(product, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException(); }
    }
}
