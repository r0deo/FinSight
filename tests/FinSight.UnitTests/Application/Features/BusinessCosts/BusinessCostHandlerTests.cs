using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Features.BusinessCosts;
using FinSight.Api.Website.Models;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.UnitTests.Application.Features.Products;

namespace FinSight.UnitTests.Application.Features.BusinessCosts;
public class BusinessCostHandlerTests
{
    [Fact]
    public async Task CommandsAndReads_UseContextTenantAndRepository_AndNeverAcceptTenantInput()
    {
        var tenant = Guid.NewGuid(); var user = new StubCurrentUserContext(Guid.NewGuid(), tenant);
        var repo = new Repository();
        var handler = new BusinessCostHandlers(repo, user);
        var details = new CostDetails("Rent", BusinessCostCategory.Rent, 100, new(2026,10,1), CostCertainty.Actual, UnitCostInclusion.No, null);
        var id = await handler.Handle(new CreateBusinessCostCommand(details), default);
        Assert.Equal(tenant, repo.Cost!.BusinessId); Assert.Equal(1, repo.Saves);
        var result = await handler.Handle(new GetBusinessCostQuery(id), default);
        Assert.Equal(tenant, repo.RequestedTenant); Assert.False(repo.Tracked); Assert.Equal(100,result.Amount);
        await handler.Handle(new UpdateBusinessCostCommand(id, 1, details with { Amount = 200 }), default);
        Assert.True(repo.Tracked); Assert.Equal(200, repo.Cost.Amount); Assert.Equal(2, repo.Cost.Version);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => handler.Handle(new DeleteBusinessCostCommand(id,1), default));
        Assert.False(repo.Removed);
        await handler.Handle(new DeleteBusinessCostCommand(id,2), default);
        Assert.True(repo.Removed);
        foreach(var type in new[] { typeof(CostDetails), typeof(BusinessCostForm), typeof(CreateBusinessCostCommand),
            typeof(UpdateBusinessCostCommand), typeof(DeleteBusinessCostCommand), typeof(GetBusinessCostsQuery) })
            Assert.Null(type.GetProperty("BusinessId"));
    }
    [Fact]
    public async Task CrossTenantCosts_AreUnavailableForReadsAndMutations()
    {
        var repo = new Repository { Cost = new(Guid.NewGuid(), Guid.NewGuid(),"Rent",BusinessCostCategory.Rent,
            100,new(2026,10,1),CostCertainty.Actual,UnitCostInclusion.No,null) };
        var tenant = Guid.NewGuid();
        var handler = new BusinessCostHandlers(repo,new StubCurrentUserContext(Guid.NewGuid(),tenant));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetBusinessCostQuery(repo.Cost.Id),default));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteBusinessCostCommand(repo.Cost.Id,1),default));
        var result = await handler.Handle(new GetBusinessCostsQuery(new(2026,10,1)),default);
        Assert.Empty(result.Entries); Assert.Equal(tenant,repo.RequestedTenant); Assert.Equal(0,repo.Saves);
    }
    private sealed class Repository : IBusinessCostRepository
    {
        public BusinessCost? Cost; public BusinessCostMonth? Month; public Guid RequestedTenant; public bool Tracked; public bool Removed; public int Saves;
        public Task AddAsync(BusinessCost cost, CancellationToken ct) { Cost=cost; return Task.CompletedTask; }
        public async Task<(IReadOnlyList<BusinessCost> Entries, BusinessCostMonth? Review)> GetMonthSnapshotAsync(Guid tenant, DateOnly month, CancellationToken ct) =>
            (await GetMonthAsync(tenant, month, ct), null);
        public Task<BusinessCostMonth?> GetTrackedMonthAsync(Guid tenant, DateOnly month, CancellationToken ct)
        { RequestedTenant = tenant; return Task.FromResult(Month?.BusinessId == tenant && Month.Month == month ? Month : null); }
        public Task AddMonthAsync(BusinessCostMonth month, CancellationToken ct) { Month = month; return Task.CompletedTask; }
        public Task<BusinessCost?> GetAsync(Guid id,Guid tenant,bool tracked,CancellationToken ct)
        { RequestedTenant=tenant; Tracked=tracked; return Task.FromResult(Cost?.BusinessId==tenant && Cost.Id==id ? Cost : null); }
        public Task<IReadOnlyList<BusinessCost>> GetMonthAsync(Guid tenant,DateOnly month,CancellationToken ct)
        { RequestedTenant=tenant; return Task.FromResult<IReadOnlyList<BusinessCost>>(Cost?.BusinessId==tenant && Cost.Month==month ? [Cost] : []); }
        public void Remove(BusinessCost cost) => Removed=true;
        public Task SaveChangesAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
    }
    [Fact]
    public async Task Review_EnforcesOwnerWithinApplication_UsesAuthenticatedTenantAndRejectsStaleVersion()
    {
        var tenant = Guid.NewGuid(); var owner = Guid.NewGuid(); var month = new DateOnly(2026, 10, 1);
        var repo = new Repository { Cost = new(Guid.NewGuid(), tenant, "Rent", BusinessCostCategory.Rent,
            100, month, CostCertainty.Actual, UnitCostInclusion.No, null) };
        var manager = new ReviewBusinessCostMonthHandler(repo, new StubCurrentUserContext(owner, tenant, "Manager"), TimeProvider.System);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => manager.Handle(new(month, 0, true), default));
        Assert.Equal(0, repo.Saves);
        var handler = new ReviewBusinessCostMonthHandler(repo, new StubCurrentUserContext(owner, tenant), TimeProvider.System);
        await handler.Handle(new(month, 0, true), default);
        Assert.Equal(tenant, repo.RequestedTenant); Assert.Equal(owner, repo.Month!.ReviewedByUserId);
        Assert.Equal(DateTimeKind.Utc, repo.Month.ReviewedAtUtc!.Value.Kind); Assert.Equal(1, repo.Saves);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => handler.Handle(new(month, 0, true), default));
        Assert.Equal(1, repo.Saves);
    }
}
