using System.Net;
using System.Text.RegularExpressions;
using FinSight.Application.Common.Exceptions;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using FinSight.Domain.Services;
using FinSight.Infrastructure.Persistence;
using FinSight.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.IntegrationTests;

public partial class WebsiteTests
{
    private static Dictionary<string, string> ProductCostFields(string name) => new(ProductFields(name, "5000"))
    {
        ["CostHelper.Ingredients[0].Name"] = "Rice",
        ["CostHelper.Ingredients[0].PackQuantity"] = "1",
        ["CostHelper.Ingredients[0].PackUnit"] = "Kilogram",
        ["CostHelper.Ingredients[0].PackPrice"] = "12000",
        ["CostHelper.Ingredients[0].UsedQuantity"] = "1000",
        ["CostHelper.Ingredients[0].UsedUnit"] = "Gram",
        ["CostHelper.UsableOutput"] = "10",
        ["CostHelper.PackagingPerUnit"] = "200",
        ["CostHelper.OtherVariableCostPerUnit"] = "0"
    };

    private static string CostInput(string html, string name)
    {
        var input = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>").Value;
        return WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]*)\"").Groups[1].Value);
    }

    [Fact]
    public async Task ProductCostHelper_AddAndRemoveRows_PreservesRemainingRawInputWithoutSaving()
    {
        using var browser = Browser(); await Login(browser, await RegisterAndConfirm(browser));
        var fields = ProductCostFields($"Rows-{Guid.NewGuid():N}"); fields["costAction"] = "add";
        var added = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.Contains("name=\"CostHelper.Ingredients[1].Name\"", await added.Content.ReadAsStringAsync());
        fields["CostHelper.Ingredients[1].Name"] = "Milk";
        fields["CostHelper.Ingredients[1].PackQuantity"] = "1";
        fields["CostHelper.Ingredients[1].PackUnit"] = "Litre";
        fields["CostHelper.Ingredients[1].PackPrice"] = "1000";
        fields["CostHelper.Ingredients[1].UsedQuantity"] = "٢٠٠";
        fields["CostHelper.Ingredients[1].UsedUnit"] = "Millilitre";
        fields["costAction"] = "remove:0";
        var removed = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var html = await removed.Content.ReadAsStringAsync();
        Assert.Equal("Milk", CostInput(html, "CostHelper.Ingredients[0].Name"));
        Assert.Equal("٢٠٠", CostInput(html, "CostHelper.Ingredients[0].UsedQuantity"));
        Assert.DoesNotContain("name=\"CostHelper.Ingredients[1].Name\"", html);
        fields["CostHelper.Ingredients[1].PackQuantity"] = "not a number";
        var invalid = await Submit(browser, "/products/create", fields);
        html = await invalid.Content.ReadAsStringAsync();
        Assert.Equal("not a number", CostInput(html, "CostHelper.Ingredients[0].PackQuantity"));
        Assert.Contains("Enter a valid number without grouping separators.", html);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Products.AnyAsync(x => x.Name == fields["Name"]));
    }

    [Fact]
    public async Task ProductCostHelper_CalculateUseCancelAndSave_ReopensOnlyExplicitlySavedBreakdown()
    {
        using var browser = Browser();
        await Login(browser, await RegisterAndConfirm(browser));
        var name = $"Cost-{Guid.NewGuid():N}";
        var fields = ProductCostFields(name);
        fields["costAction"] = "calculate";
        var calculated = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.OK, calculated.StatusCode);
        var html = await calculated.Content.ReadAsStringAsync();
        Assert.Contains("1,400.00 IQD", html);
        fields["CostPreviewFingerprint"] = CostInput(html, "CostPreviewFingerprint");
        Assert.NotEmpty(fields["CostPreviewFingerprint"]);
        fields["costAction"] = "use";
        var used = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        Assert.Equal("1400", CostInput(await used.Content.ReadAsStringAsync(), "UnitCost"));
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Products.AnyAsync(x => x.Name == name));
        fields["UseCostHelper"] = "true"; fields["UnitCost"] = "1400"; fields["UnitCostBeforeHelper"] = "4";
        fields["costAction"] = "cancel";
        var cancel = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("4", CostInput(await cancel.Content.ReadAsStringAsync(), "UnitCost"));
        fields.Remove("costAction");
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, "/products/create", fields)).StatusCode);
        var product = await FindProduct(name);
        Assert.Equal(1400m, product.UnitCost);
        Assert.Equal(0m, product.CostBreakdown!.Inputs.OtherVariableCostPerUnit);
        Assert.Equal(12000m, product.CostBreakdown.Result.TotalIngredientCost);
        var url = $"/products/{product.Id}/edit";
        html = await browser.GetStringAsync(url);
        Assert.Equal("Rice", CostInput(html, "CostHelper.Ingredients[0].Name"));
        Assert.Equal("10", CostInput(html, "CostHelper.UsableOutput"));
        fields["costAction"] = "calculate"; fields["CostHelper.Ingredients[0].PackPrice"] = "18000";
        var draft = await Submit(browser, url, fields);
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        Assert.Equal(1400m, (await FindProduct(name)).UnitCost);
        fields["costAction"] = "cancel"; fields["UnitCostBeforeHelper"] = "1400";
        var cancelled = await Submit(browser, url, fields);
        html = await cancelled.Content.ReadAsStringAsync();
        Assert.Equal("12000", CostInput(html, "CostHelper.Ingredients[0].PackPrice"));
        Assert.Equal(12000m, (await FindProduct(name)).CostBreakdown!.Inputs.Ingredients[0].PackPrice);
        // Manual editing deliberately clears a stale saved breakdown in the same write.
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, url, ProductFields(name))).StatusCode);
        product = await FindProduct(name);
        Assert.Equal(4m, product.UnitCost);
        Assert.Null(product.CostBreakdown);
    }

    [Theory]
    [InlineData("en", "1000", "200")]
    [InlineData("ar-IQ", "١٠٠٠", "٢٠٠")]
    [InlineData("ku-Arab-IQ", "۱۰۰۰", "۲۰۰")]
    public async Task ProductCostHelper_LocalizedNumbersMissingFieldsAndStaleReview_AreHandled(string culture, string used, string packaging)
    {
        using var browser = Browser();
        await Login(browser, await RegisterAndConfirm(browser));
        await Submit(browser, "/culture", new() { ["culture"] = culture, ["returnUrl"] = "/products/create" }, "/products/create");
        var fields = ProductCostFields("Localized ingredients");
        fields["costAction"] = "calculate";
        fields["CostHelper.Ingredients[0].UsedQuantity"] = used;
        fields["CostHelper.PackagingPerUnit"] = packaging;
        var preview = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var html = await preview.Content.ReadAsStringAsync();
        Assert.Contains("data-product-cost-result", html);
        Assert.Contains($"dir=\"{(culture == "en" ? "ltr" : "rtl")}\"", html);
        fields["CostPreviewFingerprint"] = CostInput(html, "CostPreviewFingerprint");
        fields["costAction"] = "use";
        fields["CostHelper.Ingredients[0].PackPrice"] = "13000";
        var stale = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        fields["costAction"] = "calculate";
        foreach (var key in new[] { "CostHelper.Ingredients[0].PackPrice", "CostHelper.Ingredients[0].PackQuantity", "CostHelper.UsableOutput" })
        {
            var missing = new Dictionary<string, string>(fields) { [key] = "" };
            Assert.Equal(HttpStatusCode.BadRequest, (await Submit(browser, "/products/create", missing)).StatusCode);
        }
        fields["CostHelper.Ingredients[0].UsedUnit"] = "Litre";
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(browser, "/products/create", fields)).StatusCode);
        fields["CostHelper.Ingredients[0].UsedUnit"] = "Gram";
        fields["CostHelper.PackagingPerUnit"] = ""; fields["CostHelper.OtherVariableCostPerUnit"] = "";
        preview = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        // Invalid helper drafts must not prevent a deliberate manual save.
        fields.Remove("costAction"); fields["CostHelper.Ingredients[0].PackQuantity"] = "nonsense";
        fields["Name"] = $"Manual-{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, "/products/create", fields)).StatusCode);
        Assert.Null((await FindProduct(fields["Name"])).CostBreakdown);
    }

    [Fact]
    public async Task ProductCostHelper_TenantIsolationAntiforgeryAndSnapshots_ArePreserved()
    {
        using var a = Browser(); using var b = Browser();
        await Login(a, await RegisterAndConfirm(a)); await Login(b, await RegisterAndConfirm(b));
        var name = $"Snapshot-{Guid.NewGuid():N}";
        await Submit(a, "/products/create", ProductFields(name, "5000"));
        var product = await FindProduct(name);
        Guid simulationId; Guid whatIfId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var userId = await db.Users.Where(x => x.BusinessId == product.BusinessId).Select(x => x.Id).SingleAsync();
            var simulation = new PricingSimulation(Guid.NewGuid(), product.BusinessId, product.Id, userId,
                5000, 4, 5500, 10, 10, DateTime.UtcNow, new PricingSimulationCalculator());
            simulationId = simulation.Id; db.PricingSimulations.Add(simulation); await db.SaveChangesAsync();
            var whatIf = new WhatIfSimulation(Guid.NewGuid(), product.BusinessId, product.Id, userId, name, "IQD",
                SalesPeriod.Month, new WhatIfInputs(5000, 4, 10, CostShockInputMode.Percentage, 20, null, 0, null, null, null, null),
                DateTime.UtcNow, new WhatIfCalculator(new PricingSimulationCalculator()));
            whatIfId = whatIf.Id; db.WhatIfSimulations.Add(whatIf); await db.SaveChangesAsync();
        }
        var path = $"/products/{product.Id}/edit";
        var fields = ProductCostFields(name);
        foreach (var action in new[] { "calculate", "use", "cancel", "" })
        {
            fields["costAction"] = action;
            Assert.Equal(HttpStatusCode.NotFound, (await Submit(b, path, fields, "/products/create")).StatusCode);
        }
        fields.Remove("costAction"); fields.Remove("__RequestVerificationToken");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsync(path, new FormUrlEncodedContent(fields))).StatusCode);
        fields["UseCostHelper"] = "true"; fields["UnitCost"] = "1";
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(a, path, fields)).StatusCode);
        Assert.Equal(4m, (await FindProduct(name)).UnitCost);
        fields["UnitCost"] = "1400"; fields["BusinessId"] = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(a, path, fields)).StatusCode);
        Assert.Equal(product.BusinessId, (await FindProduct(name)).BusinessId);
        var preview = await Submit(a, "/simulations/preview", new()
        {
            ["ProductId"] = product.Id.ToString(), ["ProposedPrice"] = "5500",
            ["CurrentSalesQuantity"] = "10", ["ExpectedSalesQuantity"] = "10"
        }, $"/simulations/preview?productId={product.Id}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Contains("36,000.00", await preview.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var snapshot = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.SingleAsync(x => x.Id == simulationId);
            Assert.Equal(4m, snapshot.UnitCost);
            Assert.Equal(49960m, snapshot.CurrentContribution);
            Assert.Equal(54960m, snapshot.ProposedContribution);
            Assert.Equal(1, snapshot.Version);
            var whatIf = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().WhatIfSimulations.SingleAsync(x => x.Id == whatIfId);
            Assert.Equal(4m, whatIf.Inputs.CurrentUnitCost);
            Assert.Equal(49952m, whatIf.Result.NoActionContribution);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductCostHelper_ConcurrentManualCostAndBreakdownWrites_CannotCreateInconsistentPair(bool helperWins)
    {
        using var browser = Browser(); await Login(browser, await RegisterAndConfirm(browser));
        var name = $"Concurrent-{Guid.NewGuid():N}";
        var fields = ProductFields(name); fields["UnitCost"] = "1400";
        await Submit(browser, "/products/create", fields);
        var product = await FindProduct(name);
        using var first = factory.Services.CreateScope(); using var second = factory.Services.CreateScope();
        var a = new ProductRepository(first.ServiceProvider.GetRequiredService<FinSightDbContext>());
        var b = new ProductRepository(second.ServiceProvider.GetRequiredService<FinSightDbContext>());
        var p1 = (await a.GetByIdForUpdateAsync(product.Id, product.BusinessId))!;
        var p2 = (await b.GetByIdForUpdateAsync(product.Id, product.BusinessId))!;
        p1.ApplyCostBreakdown(new ProductCostInputs([new("Rice", 1, IngredientUnit.Kilogram, 12000, 1000, IngredientUnit.Gram)], 10, 200, 0));
        p2.UpdateFinancials(p2.SellingPrice, 1500);
        if (helperWins)
        {
            await a.SaveChangesAsync();
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => b.SaveChangesAsync());
        }
        else
        {
            await b.SaveChangesAsync();
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => a.SaveChangesAsync());
        }
        var saved = await FindProduct(name);
        Assert.Equal(helperWins ? 1400m : 1500m, saved.UnitCost);
        if (helperWins) Assert.Equal(saved.UnitCost, saved.CostBreakdown!.Result.EstimatedUnitCost);
        else Assert.Null(saved.CostBreakdown);
    }
}
