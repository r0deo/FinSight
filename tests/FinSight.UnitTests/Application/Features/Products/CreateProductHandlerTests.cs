using FinSight.Api.Contracts.Products;
using FinSight.Application.Features.Products.Commands.CreateProduct;

namespace FinSight.UnitTests.Application.Features.Products;

public class CreateProductHandlerTests
{
    [Fact]
    public async Task Handle_CreatesProductForAuthenticatedBusiness()
    {
        // Arrange
        var businessId = Guid.NewGuid();
        var repository = new StubProductRepository();
        var currentUser = new StubCurrentUserContext(Guid.NewGuid(), businessId);
        var handler = new CreateProductHandler(repository, currentUser);
        var command = new CreateProductCommand(
            " Coffee ", " Drinks ", " Large ", 10m, 4m);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(repository.AddedProduct);
        Assert.Equal(businessId, repository.AddedProduct.BusinessId);
        Assert.Equal("Coffee", repository.AddedProduct.Name);
        Assert.Equal(result.Id, repository.AddedProduct.Id);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public void ApiRequests_DoNotAcceptBusinessIdFromClient()
    {
        // Arrange
        var requestTypes = new[]
        {
            typeof(CreateProductRequest),
            typeof(UpdateProductRequest)
        };

        // Act
        var businessIdProperties = requestTypes
            .SelectMany(type => type.GetProperties())
            .Where(property => property.Name == "BusinessId");

        // Assert
        Assert.Empty(businessIdProperties);
    }
}
