using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.BusinessCosts;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Models;
using FinSight.UnitTests.Application.Features.Products;

namespace FinSight.UnitTests.Application.Features.BusinessCosts;

public sealed class SimulationCostContextTests
{
    [Fact]
    public async Task Capture_UsesAuthenticatedTenantAndCancellation_AndNeverFetchesDisabledContext()
    {
        var tenant = Guid.NewGuid(); var repo = new Repository();
        var service = new SimulationCostContextService(repo, new StubCurrentUserContext(Guid.NewGuid(), tenant));
        using var cancellation = new CancellationTokenSource();
        var off = await SimulationCostContextService.Capture(service, new(false, null, true, -10, true),
            SalesPeriod.Month, 500000, [new("Proposed", 600000)], cancellation.Token);
        Assert.Equal(0, repo.Reads); Assert.Empty(off.Entries);
        repo.Entries.Add(new(Guid.NewGuid(), tenant, "Rent", BusinessCostCategory.Rent, 1000000,
            new(2026,10,1), CostCertainty.Actual, UnitCostInclusion.No, null));
        var preview = await SimulationCostContextService.Capture(service, new(true, new(2026,10,1), true, 10, true),
            SalesPeriod.Month, 500000, [new("Proposed", 600000)], cancellation.Token);
        Assert.Equal(tenant, repo.Tenant); Assert.Equal(cancellation.Token, repo.Token);
        Assert.Equal(100000, preview.AllocationAmount);
        var saved = await SimulationCostContextService.Capture(service, preview.Settings, SalesPeriod.Month,
            500000, [new("Proposed", 600000)], cancellation.Token, preview.BasisFingerprint, true);
        Assert.Equal(preview.BasisFingerprint, saved.BasisFingerprint);
        repo.Entries[0].Update("Rent", BusinessCostCategory.Rent, 2000000, new(2026,10,1), CostCertainty.Actual, UnitCostInclusion.No, null);
        Assert.Equal(1000000, saved.RecordedBasis); Assert.Equal(100000, saved.AllocationAmount);
        await Assert.ThrowsAsync<PricingSimulationConflictException>(() => SimulationCostContextService.Capture(service,
            preview.Settings, SalesPeriod.Month, 500000, [new("Proposed", 600000)], default, preview.BasisFingerprint, true));
        await Assert.ThrowsAsync<PricingSimulationConflictException>(() => SimulationCostContextService.Capture(service,
            new(), SalesPeriod.Month, 500000, [new("Proposed", 600000)], default, preview.BasisFingerprint, true));
    }

    [Fact]
    public async Task LinkingRequiresPreviewBasis_AndValidatesBeforeQuerying()
    {
        var repo = new Repository();
        var service = new SimulationCostContextService(repo, new StubCurrentUserContext(Guid.NewGuid(), Guid.NewGuid()));
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => SimulationCostContextService.Capture(service,
            new(true), SalesPeriod.Month, 1, [], default));
        Assert.Equal(0, repo.Reads);
        await Assert.ThrowsAsync<PricingSimulationConflictException>(() => SimulationCostContextService.Capture(service,
            new(true,new(2026,10,1)), SalesPeriod.Month, 1, [], default, null, true));
        // Disabled contexts remain backward compatible with existing API/Application callers.
        var saved = await SimulationCostContextService.Capture(null, null, SalesPeriod.Month, 1, [], default, null, true);
        Assert.False(saved.Settings.Enabled);
    }

    private sealed class Repository : IBusinessCostRepository
    {
        public List<BusinessCost> Entries = []; public int Reads; public Guid Tenant; public CancellationToken Token;
        public Task<(IReadOnlyList<BusinessCost> Entries, BusinessCostMonth? Review)> GetMonthSnapshotAsync(Guid tenant, DateOnly month, CancellationToken ct)
        {
            Reads++; Tenant=tenant; Token=ct;
            return Task.FromResult<(IReadOnlyList<BusinessCost>, BusinessCostMonth?)>((Entries.Where(x => x.BusinessId==tenant && x.Month==month).ToArray(), null));
        }
        public Task<IReadOnlyList<BusinessCost>> GetMonthAsync(Guid tenant, DateOnly month, CancellationToken ct) => throw new NotSupportedException();
        public Task<BusinessCostMonth?> GetTrackedMonthAsync(Guid tenant, DateOnly month, CancellationToken ct) => throw new NotSupportedException();
        public Task AddMonthAsync(BusinessCostMonth month, CancellationToken ct) => throw new NotSupportedException();
        public Task<BusinessCost?> GetAsync(Guid id, Guid tenant, bool tracked, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(BusinessCost cost, CancellationToken ct) => throw new NotSupportedException();
        public void Remove(BusinessCost cost) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
