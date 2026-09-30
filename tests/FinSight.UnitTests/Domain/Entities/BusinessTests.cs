using FinSight.Domain.Entities;

namespace FinSight.UnitTests.Domain.Entities;

public class BusinessTests
{
    [Fact]
    public void Constructor_WithValidData_NormalizesRequiredValues()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var business = new Business(
            id,
            "  FinSight Coffee  ",
            "  Baghdad  ",
            "usd",
            "  owner@example.com  ");

        // Assert
        Assert.Equal(id, business.Id);
        Assert.Equal("FinSight Coffee", business.Name);
        Assert.Equal("Baghdad", business.Location);
        Assert.Equal("USD", business.Currency);
        Assert.Equal("owner@example.com", business.ContactInformation);
    }

    [Fact]
    public void Constructor_WithMissingRequiredValue_ThrowsArgumentException()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        Action act = () => new Business(
            id,
            string.Empty,
            "Baghdad",
            "USD",
            "owner@example.com");

        // Assert
        Assert.Throws<ArgumentException>(act);
    }
}
