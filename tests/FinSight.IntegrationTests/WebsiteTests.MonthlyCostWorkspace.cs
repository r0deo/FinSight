using System.Net;
using FinSight.Application.Common.Exceptions;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using FinSight.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.IntegrationTests;

public partial class WebsiteTests
{
    private const string CostMonthPage = "/business-costs?month=2026-10";
    private static Dictionary<string, string> ReviewFields(long version, bool reviewed = true, string month = "2026-10") =>
        new() { ["month"] = month, ["version"] = version.ToString(), ["reviewed"] = reviewed.ToString() };
    private async Task<BusinessCost> FindCost(string name)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().BusinessCosts.AsNoTracking().SingleAsync(x => x.Name == name);
    }
    private async Task<BusinessCostMonth> FindCostMonth(Guid tenant, int month = 10)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().BusinessCostMonths.AsNoTracking()
            .SingleAsync(x => x.BusinessId == tenant && x.Month == new DateOnly(2026, month, 1));
    }

    [Fact]
    public async Task Costs_EmptyWorkspaceExplainsUnknownCosts_AndCannotDeclareEmptyMonthComplete()
    {
        using var browser = Browser(); await Login(browser, await RegisterAndConfirm(browser));
        var html = await browser.GetStringAsync(CostMonthPage);
        Assert.Contains("Add your first cost", html); Assert.Contains("Month not yet reviewed", html);
        Assert.Contains("missing entries do not mean zero expenses", html);
        Assert.DoesNotContain("data-cost-adjustment>", html);
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(browser, "/business-costs/review", ReviewFields(0), CostMonthPage)).StatusCode);
    }

    [Fact]
    public async Task Costs_ReviewAndReopen_RequireOwnerAntiforgeryAndCurrentVersion_AndAreTenantSafe()
    {
        using var owner = Browser(); using var other = Browser(); using var manager = Browser();
        await Login(owner, await RegisterAndConfirm(owner)); await Login(other, await RegisterAndConfirm(other));
        var managerEmail = await RegisterAndConfirm(manager);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByEmailAsync(managerEmail))!;
            Assert.True((await users.RemoveFromRoleAsync(user, "Owner")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Manager")).Succeeded);
        }
        await Login(manager, managerEmail);
        var name = Guid.NewGuid().ToString("N");
        await Submit(owner, "/business-costs/create", CostFields(name));
        var cost = await FindCost(name);
        var month = await FindCostMonth(cost.BusinessId);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/business-costs/review", new FormUrlEncodedContent(ReviewFields(month.Version)))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(owner, "/business-costs/review", ReviewFields(month.Version), CostMonthPage)).StatusCode);
        Assert.Contains("Reviewed by owner", await owner.GetStringAsync(CostMonthPage));
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(owner, "/business-costs/review", ReviewFields(month.Version), CostMonthPage)).StatusCode);
        var reviewed = await FindCostMonth(cost.BusinessId);
        Assert.NotNull(reviewed.ReviewedAtUtc); Assert.NotNull(reviewed.ReviewedByUserId);
        var forged = ReviewFields(0); forged["BusinessId"] = cost.BusinessId.ToString();
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(other, "/business-costs/review", forged, CostMonthPage)).StatusCode);
        Assert.Contains("Month not yet reviewed", await other.GetStringAsync(CostMonthPage));
        Assert.Equal(reviewed.Version, (await FindCostMonth(cost.BusinessId)).Version);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(manager, "/business-costs/create", CostFields("Manager-" + name))).StatusCode);
        var forbidden = await Submit(manager, "/business-costs/review", ReviewFields(1), CostMonthPage);
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.Contains("/account/access-denied", forbidden.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync(forbidden.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(owner, "/business-costs/review", ReviewFields(reviewed.Version, false), CostMonthPage)).StatusCode);
        Assert.Null((await FindCostMonth(cost.BusinessId)).ReviewedAtUtc);
    }

    [Fact]
    public async Task Costs_AddEditMoveAndDelete_ReopenAffectedMonths()
    {
        using var browser = Browser(); await Login(browser, await RegisterAndConfirm(browser));
        var name = Guid.NewGuid().ToString("N");
        await Submit(browser, "/business-costs/create", CostFields(name));
        var cost = await FindCost(name);
        async Task Review(int month = 10)
        {
            var state = await FindCostMonth(cost.BusinessId, month);
            Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, "/business-costs/review",
                ReviewFields(state.Version, month: $"2026-{month:00}"), CostMonthPage)).StatusCode);
        }
        await Review();
        await Submit(browser, "/business-costs/create", CostFields(name + "2"));
        Assert.Null((await FindCostMonth(cost.BusinessId)).ReviewedAtUtc);
        await Review();
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, $"/business-costs/{cost.Id}/edit", CostFields(name, "200", 1))).StatusCode);
        Assert.Null((await FindCostMonth(cost.BusinessId)).ReviewedAtUtc);
        var november = CostFields(name + "3"); november["Month"] = "2026-11";
        await Submit(browser, "/business-costs/create", november);
        await Review(); await Review(11);
        var moved = CostFields(name, "200", 2); moved["Month"] = "2026-11";
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, $"/business-costs/{cost.Id}/edit", moved)).StatusCode);
        Assert.Null((await FindCostMonth(cost.BusinessId)).ReviewedAtUtc);
        Assert.Null((await FindCostMonth(cost.BusinessId, 11)).ReviewedAtUtc);
        await Review(11);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, $"/business-costs/{cost.Id}/delete", new() { ["version"] = "3" })).StatusCode);
        Assert.Null((await FindCostMonth(cost.BusinessId, 11)).ReviewedAtUtc);
    }

    [Fact]
    public async Task Costs_AdjustmentDoesNotWriteAnything_RejectsInvalidInputsAndUsesOnlyCurrentTenant()
    {
        using var a = Browser(); using var b = Browser();
        await Login(a, await RegisterAndConfirm(a)); await Login(b, await RegisterAndConfirm(b));
        var name = Guid.NewGuid().ToString("N");
        await Submit(a, "/business-costs/create", CostFields(name, "120"));
        var inventory = CostFields(name + "Inventory", "900"); inventory["Category"] = "Inventory";
        await Submit(a, "/business-costs/create", inventory);
        var cost = await FindCost(name); var state = await FindCostMonth(cost.BusinessId);
        var fields = new Dictionary<string,string> { ["Month"] = "2026-10", ["Category"] = "Rent", ["Percent"] = "25", ["BusinessId"] = cost.BusinessId.ToString() };
        var result = await Submit(a, "/business-costs/adjustment", fields, CostMonthPage);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var html = WebUtility.HtmlDecode(await result.Content.ReadAsStringAsync());
        Assert.Contains("Conditional result", html); Assert.Contains("30.00 IQD", html); Assert.Contains("90.00 IQD", html);
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(b, "/business-costs/adjustment", fields, CostMonthPage)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync("/business-costs/adjustment", new FormUrlEncodedContent(
            fields.Where(x => x.Key != "__RequestVerificationToken")))).StatusCode);
        foreach (var (key, value) in new[] { ("Percent", "-1"), ("Percent", "101"), ("Percent", "1.001"), ("Category", "Inventory"), ("Category", "999"), ("Category", "Energy") })
        {
            var invalid = new Dictionary<string,string>(fields) { [key] = value };
            Assert.Equal(HttpStatusCode.BadRequest, (await Submit(a, "/business-costs/adjustment", invalid, CostMonthPage)).StatusCode);
        }
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
        Assert.Equal(1020, await db.BusinessCosts.Where(x => x.BusinessId == cost.BusinessId).SumAsync(x => x.Amount));
        Assert.Equal(1, (await FindCost(name)).Version); Assert.Equal(state.Version, (await FindCostMonth(cost.BusinessId)).Version);
        Assert.False(await db.WhatIfSimulations.AnyAsync(x => x.BusinessId == cost.BusinessId));
        Assert.False(await db.PricingSimulations.AnyAsync(x => x.BusinessId == cost.BusinessId));
    }

    [Fact]
    public async Task Costs_Comparison_HandlesZeroBaselineEstimatesAndReviewedMonths()
    {
        using var browser = Browser(); await Login(browser, await RegisterAndConfirm(browser));
        var name = Guid.NewGuid().ToString("N");
        var previous = CostFields(name, "0"); previous["Month"] = "2026-09";
        await Submit(browser, "/business-costs/create", previous);
        var current = CostFields(name + "current", "120"); current["Certainty"] = "Estimated";
        await Submit(browser, "/business-costs/create", current);
        var html = await browser.GetStringAsync(CostMonthPage);
        Assert.Contains("previous recorded operating amount is zero", html);
        Assert.Contains("either month is not reviewed or contains estimates", html);
        var old = await FindCost(name);
        previous["Amount"] = "100"; previous["Version"] = "1";
        await Submit(browser, $"/business-costs/{old.Id}/edit", previous);
        var changed = await browser.GetStringAsync(CostMonthPage);
        Assert.Contains("20.00%", changed);
        foreach (var month in new[] { 9, 10 })
        {
            var state = await FindCostMonth(old.BusinessId, month);
            await Submit(browser, "/business-costs/review", ReviewFields(state.Version, month: $"2026-{month:00}"), CostMonthPage);
        }
        Assert.Contains("either month is not reviewed or contains estimates", await browser.GetStringAsync(CostMonthPage));
    }

    [Fact]
    public async Task Costs_DatabaseConcurrency_PreventsStaleReviewAndRollsBackLosingCostWrite()
    {
        using var browser = Browser(); await Login(browser, await RegisterAndConfirm(browser));
        var name = Guid.NewGuid().ToString("N"); await Submit(browser, "/business-costs/create", CostFields(name));
        var original = await FindCost(name);
        using var scope1 = factory.Services.CreateScope(); using var scope2 = factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<FinSightDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<FinSightDbContext>();
        var repo1 = new BusinessCostRepository(db1); var repo2 = new BusinessCostRepository(db2);
        var month1 = (await repo1.GetTrackedMonthAsync(original.BusinessId, original.Month, default))!;
        var month2 = (await repo2.GetTrackedMonthAsync(original.BusinessId, original.Month, default))!;
        var row = (await repo2.GetAsync(original.Id, original.BusinessId, true, default))!;
        month1.Review(Guid.NewGuid(), DateTime.UtcNow); await repo1.SaveChangesAsync(default);
        row.Update(row.Name, row.Category, 999, row.Month, row.Certainty, row.Inclusion, row.Note);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repo2.SaveChangesAsync(default));
        Assert.Equal(100, (await FindCost(name)).Amount);
        Assert.NotNull((await FindCostMonth(original.BusinessId)).ReviewedAtUtc);
        db1.ChangeTracker.Clear(); db2.ChangeTracker.Clear();
        var staleReview = (await repo1.GetTrackedMonthAsync(original.BusinessId, original.Month, default))!;
        row = (await repo2.GetAsync(original.Id, original.BusinessId, true, default))!;
        row.Update(row.Name, row.Category, 200, row.Month, row.Certainty, row.Inclusion, row.Note);
        await repo2.SaveChangesAsync(default);
        staleReview.Review(Guid.NewGuid(), DateTime.UtcNow);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repo1.SaveChangesAsync(default));
        Assert.Equal(200, (await FindCost(name)).Amount); Assert.Null((await FindCostMonth(original.BusinessId)).ReviewedAtUtc);
    }
}
