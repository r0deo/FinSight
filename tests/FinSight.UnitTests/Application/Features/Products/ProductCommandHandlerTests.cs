using FinSight.Application.Features.Products.Commands.DeactivateProduct;
using FinSight.Application.Features.Products.Commands.ReactivateProduct;
using FinSight.Application.Features.Products.Commands.UpdateProduct;
using FinSight.Domain.Entities;

namespace FinSight.UnitTests.Application.Features.Products;

public class ProductCommandHandlerTests
{
    [Fact]
    public async Task Update_UsesProductDomainMethodsAndSavesChanges()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var product = CreateProduct(businessId);
        var repository = new StubProductRepository { TrackedProduct = product };
        var handler = new UpdateProductHandler(
            repository,
            new StubCurrentUserContext(Guid.NewGuid(), businessId));
        var command = new UpdateProductCommand(
            product.Id, " Tea ", " Hot Drinks ", " Small ", 8m, 3m);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal("Tea", product.Name);
        Assert.Equal("Hot Drinks", product.Category);
        Assert.Equal("Small", product.Size);
        Assert.Equal(8m, product.SellingPrice);
        Assert.Equal(3m, product.UnitCost);
        Assert.Equal(businessId, repository.RequestedBusinessId);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Deactivate_UsesDomainMethodAndSavesChanges()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var product = CreateProduct(businessId);
        var repository = new StubProductRepository { TrackedProduct = product };
        var handler = new DeactivateProductHandler(
            repository,
            new StubCurrentUserContext(Guid.NewGuid(), businessId));

        // Act
        await handler.Handle(
            new DeactivateProductCommand(product.Id),
            CancellationToken.None);

        // Assert
        Assert.False(product.IsActive);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Reactivate_UsesDomainMethodAndSavesChanges()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var product = CreateProduct(businessId);
        product.Deactivate();
        var repository = new StubProductRepository { TrackedProduct = product };
        var handler = new ReactivateProductHandler(
            repository,
            new StubCurrentUserContext(Guid.NewGuid(), businessId));

        // Act
        await handler.Handle(
            new ReactivateProductCommand(product.Id),
            CancellationToken.None);

        // Assert
        Assert.True(product.IsActive);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    private static Product CreateProduct(Guid businessId) => new(
        Guid.NewGuid(), businessId, "Coffee", "Drinks", null, 10m, 4m);
}
