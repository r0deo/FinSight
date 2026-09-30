using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.PricingSimulations.Queries.PreviewPricingSimulation;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Services;

namespace FinSight.UnitTests.Application.Features.PricingSimulations;

public class PreviewPricingSimulationHandlerTests
{
    [Fact]
    public async Task Handle_WithExistingProduct_ReturnsSimulationForCurrentBusiness()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var product = new Product(
            productId,
            businessId,
            "Coffee",
            "Drinks",
            null,
            10m,
            4m);
        var repository = new StubProductRepository(product);
        var currentUser = new StubCurrentUserContext(Guid.NewGuid(), businessId);
        var handler = new PreviewPricingSimulationHandler(
            repository,
            currentUser,
            new PricingSimulationCalculator());
        var query = new PreviewPricingSimulationQuery(productId, 12m, 5, 6);
        using var cancellationSource = new CancellationTokenSource();

        // Act
        var response = await handler.Handle(query, cancellationSource.Token);

        // Assert
        Assert.Equal(productId, repository.RequestedProductId);
        Assert.Equal(businessId, repository.RequestedBusinessId);
        Assert.Equal(cancellationSource.Token, repository.CancellationToken);
        Assert.Equal(30m, response.CurrentContribution);
        Assert.Equal(48m, response.ProposedContribution);
        Assert.Equal(18m, response.Difference);
        Assert.Equal(Recommendation.Recommended, response.Recommendation);
        Assert.False(response.IsProposedPriceBelowCost);
    }

    [Fact]
    public async Task Handle_WithUnavailableProduct_ThrowsNotFoundException()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var repository = new StubProductRepository(null);
        var currentUser = new StubCurrentUserContext(Guid.NewGuid(), businessId);
        var handler = new PreviewPricingSimulationHandler(
            repository,
            currentUser,
            new PricingSimulationCalculator());
        var query = new PreviewPricingSimulationQuery(productId, 12m, 5, 6);

        // Act
        var act = () => handler.Handle(query, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(act);
        Assert.Equal(productId, repository.RequestedProductId);
        Assert.Equal(businessId, repository.RequestedBusinessId);
    }

    private sealed class StubProductRepository(Product? product)
        : IProductRepository
    {
        public Guid? RequestedProductId { get; private set; }

        public Guid? RequestedBusinessId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<Product?> GetByIdAsync(
            Guid productId,
            Guid businessId,
            CancellationToken cancellationToken = default)
        {
            RequestedProductId = productId;
            RequestedBusinessId = businessId;
            CancellationToken = cancellationToken;

            return Task.FromResult(product);
        }

        public Task AddAsync(
            Product product,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record StubCurrentUserContext(
        Guid UserId,
        Guid BusinessId,
        string Role = "Owner") : ICurrentUserContext;
}
