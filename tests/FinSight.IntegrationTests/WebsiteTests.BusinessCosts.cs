using System.Net;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Application.Features.BusinessCosts;
using FinSight.Infrastructure.Persistence;
using FinSight.Infrastructure.Persistence.Repositories;
using FinSight.Application.Common.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
namespace FinSight.IntegrationTests;

public partial class WebsiteTests
{
    [Fact]
    public async Task WhatIf_StaleHiddenInputsAreIgnored_AndMonthlyContextNeverChangesContribution()
    {
        using var browser = Browser();
        await Login(browser, await RegisterAndConfirm(browser));
        var name = "Concern-" + Guid.NewGuid().ToString("N");
        var productFields = ProductFields(name, "4000"); productFields["UnitCost"] = "1000";
        await Submit(browser, "/products/create", productFields);
        var product = await FindProduct(name);
        await Submit(browser, "/business-costs/create", CostFields(name, "12345"));
        var url = $"/what-if?productId={product.Id}";
        var fields = new Dictionary<string,string>
        {
            ["ProductId"] = product.Id.ToString(), ["CurrentQuantity"] = "100",
            ["Concern"] = "Supplier", ["Period"] = "Month", ["ContextMonth"] = "2026-10",
            ["CostInputMode"] = "Percentage", ["CostIncreasePercent"] = "20",
            ["SalesDeclinePercent"] = "stale-hidden", ["NewUnitCost"] = "stale-hidden",
            ["AlternativePrice"] = "stale-hidden", ["AlternativePriceQuantity"] = "stale-hidden"
        };
        var response = await Submit(browser, "/what-if/preview", fields, url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("280,000.00", html); Assert.Contains("12,345.00", html);
        Assert.Contains("not part of the saved calculation snapshot", html);
        fields["Concern"] = "Demand"; fields["SalesDeclinePercent"] = "10";
        fields["CostIncreasePercent"] = "stale-hidden"; fields["CostInputMode"] = "invalid-hidden";
        response = await Submit(browser, "/what-if/preview", fields, url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("270,000.00", await response.Content.ReadAsStringAsync());
        fields["Period"] = "Week";
        response = await Submit(browser, "/what-if/preview", fields, url);
        Assert.DoesNotContain("12,345.00", await response.Content.ReadAsStringAsync());
        fields["Concern"] = "999";
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(browser, "/what-if/preview", fields, url)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
        Assert.False(await db.WhatIfSimulations.AnyAsync(x => x.ProductId == product.Id));
        Assert.False(await db.PricingSimulations.AnyAsync(x => x.ProductId == product.Id));
        Assert.Equal(12345, await db.BusinessCosts.Where(x => x.Name == name).Select(x => x.Amount).SingleAsync());
        var unchanged = await db.Products.SingleAsync(x => x.Id == product.Id);
        Assert.Equal(1000, unchanged.UnitCost); Assert.Equal(4000, unchanged.SellingPrice);
    }

    [Fact]
    public async Task BusinessCosts_RequireLogin_AndPreserveMonthDuringCultureSwitch()
    {
        using var browser = Browser();
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/business-costs")).StatusCode);
        await Login(browser, await RegisterAndConfirm(browser));
        var response = await Submit(browser, "/culture", new() { ["culture"] = "ar-IQ",
            ["returnUrl"] = "/business-costs?month=2026-11" }, "/business-costs?month=2026-11");
        Assert.Equal("/business-costs?month=2026-11", response.Headers.Location!.OriginalString);
        var html = WebUtility.HtmlDecode(await browser.GetStringAsync(response.Headers.Location));
        Assert.Contains("تكاليف العمل", html);
        Assert.Contains("2026-11", html);
    }
    [Fact]
    public async Task BusinessCosts_CrudMonthIsolationTenantSafetyAndConcurrency_ArePersisted()
    {
        using var a = Browser(); using var b = Browser();
        await Login(a, await RegisterAndConfirm(a)); await Login(b, await RegisterAndConfirm(b));
        var name = "Rent-" + Guid.NewGuid().ToString("N");
        var fields = CostFields(name);
        fields["BusinessId"] = Guid.NewGuid().ToString(); // ignored, never a trusted tenant input
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(a, "/business-costs/create", fields)).StatusCode);
        BusinessCost cost;
        using(var scope = factory.Services.CreateScope())
            cost = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().BusinessCosts.AsNoTracking().SingleAsync(x => x.Name == name);
        Assert.NotEqual(Guid.Parse(fields["BusinessId"]), cost.BusinessId);
        Assert.Contains(name, await a.GetStringAsync("/business-costs?month=2026-10"));
        Assert.DoesNotContain(name, await a.GetStringAsync("/business-costs?month=2026-11"));
        Assert.DoesNotContain(name, await b.GetStringAsync("/business-costs?month=2026-10"));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/business-costs/{cost.Id}/edit")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Submit(b, $"/business-costs/{cost.Id}/edit", CostFields(name, "900", 1), "/business-costs/create")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Submit(b, $"/business-costs/{cost.Id}/delete", new() { ["version"] = "1" }, "/business-costs/create")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync($"/business-costs/{cost.Id}/delete", new FormUrlEncodedContent(new Dictionary<string,string> { ["version"] = "1" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(a, $"/business-costs/{cost.Id}/edit", CostFields(name, "250.50", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(a, $"/business-costs/{cost.Id}/edit", CostFields(name, "999", 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(a, $"/business-costs/{cost.Id}/delete", new() { ["version"] = "1" })).StatusCode);
        using(var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var row = await db.BusinessCosts.AsNoTracking().SingleAsync(x => x.Id == cost.Id);
            Assert.Equal(250.50m, row.Amount); Assert.Equal(2, row.Version);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(a, $"/business-costs/{cost.Id}/delete", new() { ["version"] = "2" })).StatusCode);
        using(var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().BusinessCosts.AnyAsync(x => x.Id == cost.Id));
    }

    [Fact]
    public async Task BusinessCosts_RealPostgresTotals_SeparateCategoryMonthCertaintyAndInclusion()
    {
        using var browser = Browser(); await Login(browser, await RegisterAndConfirm(browser));
        var suffix = Guid.NewGuid().ToString("N");
        foreach (var (category, amount, certainty, inclusion, month) in new[] {
            ("Rent", "100", "Actual", "No", "2026-10"),
            ("Rent", "20", "Estimated", "Yes", "2026-10"),
            ("Inventory", "300", "Actual", "Unsure", "2026-10"),
            ("Energy", "50", "Estimated", "Unsure", "2026-10"),
            ("Rent", "900", "Actual", "No", "2026-11") })
        {
            var f = CostFields(suffix + category + amount, amount);
            f["Category"] = category; f["Certainty"] = certainty; f["Inclusion"] = inclusion; f["Month"] = month;
            Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, "/business-costs/create", f)).StatusCode);
        }
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
        var tenant = await db.BusinessCosts.Where(x => x.Name == suffix + "Rent100").Select(x => x.BusinessId).SingleAsync();
        var repo = new BusinessCostRepository(db);
        var rows = await repo.GetMonthAsync(tenant, new(2026,10,1), default);
        var summary = new BusinessCostSummary(new(2026,10,1), rows.Select(BusinessCostResponse.From).ToArray());
        Assert.Equal(4, rows.Count);
        Assert.Equal(2, summary.OperatingCategories.Count);
        Assert.Equal(new CategoryTotal(BusinessCostCategory.Rent,100,20), summary.OperatingCategories[0]);
        Assert.Equal(new CategoryTotal(BusinessCostCategory.Energy,0,50), summary.OperatingCategories[1]);
        Assert.Equal(300, summary.InventoryActual); Assert.Equal(0, summary.InventoryEstimated);
        Assert.Equal(2, summary.UnresolvedCount);
        Assert.Single(rows, x => x.Inclusion == UnitCostInclusion.Yes);
        Assert.Empty(await repo.GetMonthAsync(Guid.NewGuid(), new(2026,10,1), default));
        Assert.Contains("Inventory purchases", await browser.GetStringAsync("/business-costs?month=2026-10"));
        Assert.DoesNotContain("900.00", await browser.GetStringAsync("/business-costs?month=2026-10"));
    }

    [Fact]
    public async Task BusinessCosts_InvalidInputsRejected_AndDatabaseVersionIsAtomic()
    {
        using var browser = Browser(); await Login(browser, await RegisterAndConfirm(browser));
        var name = Guid.NewGuid().ToString("N");
        foreach(var (key, value) in new[] { ("Amount","-1"), ("Amount","1.001"), ("Month","2026-13"),
            ("Month","1999-01"), ("Category","999"), ("Inclusion","999"), ("Name"," ") })
        {
            var fields = CostFields(name); fields[key] = value;
            Assert.Equal(HttpStatusCode.BadRequest, (await Submit(browser,"/business-costs/create",fields)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser,"/business-costs/create",CostFields(name))).StatusCode);
        using var scope1 = factory.Services.CreateScope(); using var scope2 = factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<FinSightDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<FinSightDbContext>();
        var first = await db1.BusinessCosts.SingleAsync(x => x.Name == name);
        var second = await db2.BusinessCosts.SingleAsync(x => x.Id == first.Id);
        first.Update(name, first.Category, 200, first.Month, first.Certainty, first.Inclusion, null);
        second.Update(name, second.Category, 300, second.Month, second.Certainty, second.Inclusion, null);
        await new BusinessCostRepository(db1).SaveChangesAsync(default);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new BusinessCostRepository(db2).SaveChangesAsync(default));
        Assert.Equal(200, await db1.BusinessCosts.AsNoTracking().Where(x => x.Id == first.Id).Select(x => x.Amount).SingleAsync());
    }
    private static Dictionary<string,string> CostFields(string name, string amount = "100", long version = 0) => new()
    {
        ["Name"] = name, ["Category"] = "Rent", ["Amount"] = amount, ["Month"] = "2026-10",
        ["Certainty"] = "Actual", ["Inclusion"] = "No", ["Version"] = version.ToString()
    };
}
