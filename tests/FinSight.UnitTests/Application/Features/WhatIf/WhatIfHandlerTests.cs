using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Application.Features.WhatIf;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Services;
using FinSight.UnitTests.Application.Features.PricingSimulations;
using FinSight.UnitTests.Application.Features.Products;

namespace FinSight.UnitTests.Application.Features.WhatIf;

public class WhatIfHandlerTests
{
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly WhatIfCalculator _calculator = new(new PricingSimulationCalculator());

    private static WhatIfRequest Request(Guid productId) => new(productId, 100,
        SalesPeriod.Week, CostShockInputMode.Percentage, 20, null, 10,
        4500, 90, null, null);

    [Fact]
    public async Task Preview_UsesAuthenticatedBusinessAndDoesNotSave()
    {
        var product = new Product(Guid.NewGuid(), _businessId, "Coffee", "Drinks", null,
            4000, 1000);
        var products = new StubProductRepository { ReadProduct = product };
        var currencies = new StubBusinessCurrencyReader();
        var response = await new PreviewWhatIfHandler(products, currencies,
            new StubCurrentUserContext(_userId, _businessId), _calculator)
            .Handle(new PreviewWhatIfQuery(Request(product.Id)), CancellationToken.None);

        Assert.Equal(_businessId, products.RequestedBusinessId);
        Assert.Equal(_businessId, currencies.RequestedBusinessId);
        Assert.Equal(252000, response.Result.NoActionContribution);
        Assert.Equal("IQD", response.Currency);
    }

    [Fact]
    public async Task Save_RecalculatesAndStoresAuthenticatedTenantSnapshot()
    {
        var product = new Product(Guid.NewGuid(), _businessId, "Coffee", "Drinks", null,
            4000, 1000);
        var products = new StubProductRepository { ReadProduct = product };
        var simulations = new StubWhatIfRepository();
        var now = DateTimeOffset.UtcNow;
        var response = await new SaveWhatIfHandler(products, new StubBusinessCurrencyReader(),
            simulations, new StubCurrentUserContext(_userId, _businessId), _calculator,
            new FixedTimeProvider(now)).Handle(new SaveWhatIfCommand(Request(product.Id),
            4000, 1000), CancellationToken.None);

        Assert.Equal(_businessId, products.RequestedBusinessId);
        Assert.Equal(_businessId, simulations.Added!.BusinessId);
        Assert.Equal(_userId, simulations.Added.CreatedByUserId);
        Assert.Equal(1, simulations.SaveCount);
        Assert.Equal(252000, response.Result.NoActionContribution);
        Assert.Equal(now.UtcDateTime, response.CreatedAtUtc);
    }

    [Fact]
    public async Task Read_CrossTenantLookupReturnsNotFound()
    {
        var simulations = new StubWhatIfRepository();
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetWhatIfByIdHandler(simulations,
                new StubCurrentUserContext(_userId, _businessId))
                .Handle(new GetWhatIfByIdQuery(id), CancellationToken.None));
        Assert.Equal(_businessId, simulations.RequestedBusinessId);
    }

    [Fact]
    public void Validator_RejectsInvalidAmountsAndInactiveModeValues()
    {
        var validator = new WhatIfRequestValidator();
        var invalid = Request(Guid.Empty) with
        {
            CurrentQuantity = -1, SalesDeclinePercent = 101,
            NewUnitCost = 2000, AlternativePrice = -1
        };
        var result = validator.Validate(invalid);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == "ProductId");
        Assert.Contains(result.Errors, x => x.PropertyName == "CurrentQuantity");
        Assert.Contains(result.Errors, x => x.PropertyName == "SalesDeclinePercent");
        Assert.Contains(result.Errors, x => x.PropertyName == "NewUnitCost");
        Assert.False(new GetWhatIfHistoryValidator().Validate(
            new GetWhatIfHistoryQuery(int.MaxValue)).IsValid);
    }

    private sealed class StubWhatIfRepository : IWhatIfSimulationRepository
    {
        public WhatIfSimulation? Added { get; private set; }
        public Guid? RequestedBusinessId { get; private set; }
        public int SaveCount { get; private set; }
        public Task AddAsync(WhatIfSimulation simulation, CancellationToken cancellationToken)
        {
            Added = simulation;
            return Task.CompletedTask;
        }
        public Task<WhatIfSimulation?> GetByIdAsync(Guid id, Guid businessId,
            CancellationToken cancellationToken)
        {
            RequestedBusinessId = businessId;
            return Task.FromResult<WhatIfSimulation?>(null);
        }
        public Task<PagedResult<WhatIfSimulation>> GetPagedAsync(Guid businessId, int page,
            int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<WhatIfSimulation>([], 0));
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
