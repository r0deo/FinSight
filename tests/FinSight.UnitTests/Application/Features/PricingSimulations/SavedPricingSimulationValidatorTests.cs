using FinSight.Api.Contracts.PricingSimulations;
using FinSight.Application.Features.PricingSimulations.Commands.CreateSimulation;
using FinSight.Application.Features.PricingSimulations.Commands.UpdateSimulation;
using FinSight.Application.Features.PricingSimulations.Queries.GetSimulations;

namespace FinSight.UnitTests.Application.Features.PricingSimulations;

public class SavedPricingSimulationValidatorTests
{
    [Fact]
    public async Task Commands_WithInvalidClientInputs_AreRejected()
    {
        // Arrange
        var createValidator = new CreateSimulationValidator();
        var updateValidator = new UpdateSimulationValidator();

        // Act
        var createResult = await createValidator.ValidateAsync(
            new CreateSimulationCommand(Guid.Empty, -1m, -1, -1));
        var updateResult = await updateValidator.ValidateAsync(
            new UpdateSimulationCommand(Guid.Empty, -1m, -1, -1));

        // Assert
        Assert.False(createResult.IsValid);
        Assert.False(updateResult.IsValid);
        Assert.Equal(4, createResult.Errors.Select(error => error.PropertyName).Distinct().Count());
        Assert.Equal(4, updateResult.Errors.Select(error => error.PropertyName).Distinct().Count());
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Pagination_WithInvalidValues_IsRejected(int page, int pageSize)
    {
        // Arrange
        var validator = new GetSimulationsValidator();

        // Act
        var result = await validator.ValidateAsync(new GetSimulationsQuery(page, pageSize));

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ApiRequests_DoNotAcceptServerOwnedSimulationFields()
    {
        // Arrange
        var forbiddenProperties = new[]
        {
            "BusinessId",
            "CreatedByUserId",
            "CurrentContribution",
            "ProposedContribution",
            "Difference",
            "IsProposedPriceBelowCost",
            "Recommendation"
        };
        var requestTypes = new[]
        {
            typeof(CreatePricingSimulationRequest),
            typeof(UpdatePricingSimulationRequest)
        };

        // Act
        var exposedForbiddenProperties = requestTypes
            .SelectMany(type => type.GetProperties())
            .Where(property => forbiddenProperties.Contains(property.Name));

        // Assert
        Assert.Empty(exposedForbiddenProperties);
    }
}
