using FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingSimulation;

namespace FinSight.UnitTests.Application.Features.PricingSimulations;

public class PreviewPricingSimulationValidatorTests
{
    [Fact]
    public void Validate_WithValidQuery_IsValid()
    {
        // Arrange
        var validator = new PreviewPricingSimulationValidator();
        var query = new PreviewPricingSimulationQuery(
            Guid.NewGuid(),
            12m,
            5,
            6);

        // Act
        var result = validator.Validate(query);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithInvalidQuery_ReturnsErrorsForEveryInvalidProperty()
    {
        // Arrange
        var validator = new PreviewPricingSimulationValidator();
        var query = new PreviewPricingSimulationQuery(
            Guid.Empty,
            -0.01m,
            -1,
            -1);

        // Act
        var result = validator.Validate(query);
        var invalidProperties = result.Errors
            .Select(error => error.PropertyName)
            .ToHashSet();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(nameof(query.ProductId), invalidProperties);
        Assert.Contains(nameof(query.ProposedPrice), invalidProperties);
        Assert.Contains(nameof(query.CurrentSalesQuantity), invalidProperties);
        Assert.Contains(nameof(query.ExpectedSalesQuantity), invalidProperties);
    }
}
