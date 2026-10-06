using FinSight.Application.Features.Products.Commands.CreateProduct;
using FinSight.Application.Features.Products.Commands.UpdateProduct;
using FinSight.Application.Features.Products.Queries.GetProducts;

namespace FinSight.UnitTests.Application.Features.Products;

public class ProductValidatorTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Pagination_WithInvalidValues_IsRejected(int page, int pageSize)
    {
        // Arrange
        var validator = new GetProductsValidator();
        var query = new GetProductsQuery(page, pageSize);

        // Act
        var result = await validator.ValidateAsync(query);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task CreateProduct_WithInvalidRequest_IsRejected()
    {
        // Arrange
        var validator = new CreateProductValidator();
        var command = new CreateProductCommand(" ", " ", null, -1m, -1m);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        Assert.Contains(result.Errors, error => error.PropertyName == "Category");
        Assert.Contains(result.Errors, error => error.PropertyName == "SellingPrice");
        Assert.Contains(result.Errors, error => error.PropertyName == "UnitCost");
    }

    [Fact]
    public async Task UpdateProduct_WithInvalidRequest_IsRejected()
    {
        // Arrange
        var validator = new UpdateProductValidator();
        var command = new UpdateProductCommand(
            Guid.Empty, new string('x', 61), " ", null, -1m, -1m);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "ProductId");
        Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        Assert.Contains(result.Errors, error => error.PropertyName == "Category");
        Assert.Contains(result.Errors, error => error.PropertyName == "SellingPrice");
        Assert.Contains(result.Errors, error => error.PropertyName == "UnitCost");
    }
}
