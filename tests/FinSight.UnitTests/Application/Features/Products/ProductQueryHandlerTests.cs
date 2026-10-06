using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Models;
using FinSight.Application.Features.Products.Queries.GetProductById;
using FinSight.Application.Features.Products.Queries.GetProducts;
using FinSight.Domain.Entities;

namespace FinSight.UnitTests.Application.Features.Products;

public class ProductQueryHandlerTests
{
    [Fact]
    public async Task GetProducts_ReturnsCurrentBusinessProductsThroughRepository()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var product = CreateProduct(businessId);
        var repository = new StubProductRepository
        {
            PagedResult = new PagedResult<Product>([product], 1)
        };
        var handler = new GetProductsHandler(
            repository,
            new StubCurrentUserContext(Guid.NewGuid(), businessId));
        var query = new GetProductsQuery(2, 10, true);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(businessId, repository.RequestedBusinessId);
        Assert.Equal(2, repository.RequestedPage);
        Assert.Equal(10, repository.RequestedPageSize);
        Assert.True(repository.RequestedIncludeInactive);
        Assert.Single(result.Items);
        Assert.Equal(product.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task GetProducts_ByDefaultExcludesInactiveProducts()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var repository = new StubProductRepository();
        var handler = new GetProductsHandler(
            repository,
            new StubCurrentUserContext(Guid.NewGuid(), businessId));

        // Act
        await handler.Handle(new GetProductsQuery(), CancellationToken.None);

        // Assert
        Assert.False(repository.RequestedIncludeInactive);
        Assert.Equal(1, repository.RequestedPage);
        Assert.Equal(20, repository.RequestedPageSize);
    }

    [Fact]
    public async Task GetProductById_WhenProductIsOutsideTenant_ThrowsNotFoundException()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var repository = new StubProductRepository { ReadProduct = null };
        var handler = new GetProductByIdHandler(
            repository,
            new StubCurrentUserContext(Guid.NewGuid(), businessId));

        // Act
        var act = () => handler.Handle(
            new GetProductByIdQuery(productId),
            CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Equal(productId, repository.RequestedProductId);
        Assert.Equal(businessId, repository.RequestedBusinessId);
    }

    private static Product CreateProduct(Guid businessId) => new(
        Guid.NewGuid(), businessId, "Coffee", "Drinks", null, 10m, 4m);
}
