using FinSight.Domain.Entities;

namespace FinSight.UnitTests.Domain.Entities;

public class ProductTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesActiveProduct()
    {
        // Arrange
        var id = Guid.NewGuid();
        var businessId = Guid.NewGuid();

        // Act
        var product = new Product(
            id,
            businessId,
            "Coffee",
            "Drinks",
            "Large",
            5.50m,
            2.25m);

        // Assert
        Assert.Equal(id, product.Id);
        Assert.Equal(businessId, product.BusinessId);
        Assert.Equal("Coffee", product.Name);
        Assert.Equal("Drinks", product.Category);
        Assert.Equal("Large", product.Size);
        Assert.Equal(5.50m, product.SellingPrice);
        Assert.Equal(2.25m, product.UnitCost);
        Assert.True(product.IsActive);
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var businessId = Guid.NewGuid();

        // Act
        Action act = () => new Product(
            id,
            businessId,
            string.Empty,
            "Drinks",
            "Large",
            5.50m,
            2.25m);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void UpdateFinancials_WithNegativePrice_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Coffee",
            "Drinks",
            "Large",
            5.50m,
            2.25m);

        // Act
        Action act = () => product.UpdateFinancials(-0.01m, 2.25m);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        // Arrange
        var product = new Product(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Coffee",
            "Drinks",
            "Large",
            5.50m,
            2.25m);

        // Act
        product.Deactivate();

        // Assert
        Assert.False(product.IsActive);
    }
}
