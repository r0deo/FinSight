using FinSight.Domain.Enums;
using FinSight.Domain.Services;

namespace FinSight.UnitTests.Domain.Services;

public class PricingSimulationCalculatorTests
{
    [Fact]
    public void Calculate_WithProfitableChange_ReturnsRecommended()
    {
        // Arrange
        var calculator = new PricingSimulationCalculator();

        // Act
        var result = calculator.Calculate(
            currentPrice: 10m,
            proposedPrice: 12m,
            unitCost: 6m,
            currentSalesQuantity: 10,
            expectedSalesQuantity: 10);

        // Assert
        Assert.Equal(40m, result.CurrentContribution);
        Assert.Equal(60m, result.ProposedContribution);
        Assert.Equal(20m, result.Difference);
        Assert.Equal(Recommendation.Recommended, result.Recommendation);
    }

    [Fact]
    public void Calculate_WithUnprofitableChange_ReturnsNotRecommended()
    {
        // Arrange
        var calculator = new PricingSimulationCalculator();

        // Act
        var result = calculator.Calculate(
            currentPrice: 10m,
            proposedPrice: 9m,
            unitCost: 6m,
            currentSalesQuantity: 10,
            expectedSalesQuantity: 8);

        // Assert
        Assert.Equal(40m, result.CurrentContribution);
        Assert.Equal(24m, result.ProposedContribution);
        Assert.Equal(-16m, result.Difference);
        Assert.Equal(Recommendation.NotRecommended, result.Recommendation);
    }

    [Fact]
    public void Calculate_WithProposedPriceBelowCost_SetsWarning()
    {
        // Arrange
        var calculator = new PricingSimulationCalculator();

        // Act
        var result = calculator.Calculate(
            currentPrice: 10m,
            proposedPrice: 5m,
            unitCost: 6m,
            currentSalesQuantity: 10,
            expectedSalesQuantity: 20);

        // Assert
        Assert.True(result.IsBelowCost);
    }

    [Fact]
    public void Calculate_WithNegativeInput_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var calculator = new PricingSimulationCalculator();

        // Act
        Action act = () => calculator.Calculate(
            currentPrice: 10m,
            proposedPrice: -1m,
            unitCost: 6m,
            currentSalesQuantity: 10,
            expectedSalesQuantity: 10);

        // Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal("proposedPrice", exception.ParamName);
    }
}
