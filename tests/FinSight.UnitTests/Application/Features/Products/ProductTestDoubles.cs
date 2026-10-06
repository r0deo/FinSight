using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Domain.Entities;

namespace FinSight.UnitTests.Application.Features.Products;

internal sealed class StubProductRepository : IProductRepository
{
    public Product? ReadProduct { get; set; }

    public Product? TrackedProduct { get; set; }

    public PagedResult<Product> PagedResult { get; set; } = new([], 0);

    public Product? AddedProduct { get; private set; }

    public Guid? RequestedProductId { get; private set; }

    public Guid? RequestedBusinessId { get; private set; }

    public int? RequestedPage { get; private set; }

    public int? RequestedPageSize { get; private set; }

    public bool? RequestedIncludeInactive { get; private set; }

    public int SaveChangesCount { get; private set; }

    public Task<Product?> GetByIdAsync(
        Guid productId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        RequestedProductId = productId;
        RequestedBusinessId = businessId;
        return Task.FromResult(ReadProduct);
    }

    public Task<Product?> GetByIdForUpdateAsync(
        Guid productId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        RequestedProductId = productId;
        RequestedBusinessId = businessId;
        return Task.FromResult(TrackedProduct);
    }

    public Task<Product?> GetActiveByIdAsync(
        Guid productId,
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        RequestedProductId = productId;
        RequestedBusinessId = businessId;
        return Task.FromResult(ReadProduct is { IsActive: true } ? ReadProduct : null);
    }

    public Task<PagedResult<Product>> GetPagedAsync(
        Guid businessId,
        int page,
        int pageSize,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        RequestedBusinessId = businessId;
        RequestedPage = page;
        RequestedPageSize = pageSize;
        RequestedIncludeInactive = includeInactive;
        return Task.FromResult(PagedResult);
    }

    public Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        AddedProduct = product;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}

internal sealed record StubCurrentUserContext(
    Guid UserId,
    Guid BusinessId,
    string Role = "Owner") : ICurrentUserContext;
