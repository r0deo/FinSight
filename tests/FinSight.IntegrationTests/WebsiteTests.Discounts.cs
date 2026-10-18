using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Globalization;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Persistence;
using FinSight.Infrastructure.Persistence.Repositories;
using FinSight.Application.Common.Exceptions;
using FinSight.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.IntegrationTests;

public partial class WebsiteTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ar-IQ")]
    [InlineData("ku-Arab-IQ")]
    public async Task Discount_PreviewSaveEditAndTenantIsolation_UseImmutableFinancialSnapshot(string culture)
    {
        string Money(decimal amount) => amount.ToString("N2", CultureInfo.GetCultureInfo(culture));
        using var owner = Browser(); var email = await RegisterAndConfirm(owner); await Login(owner, email);
        async Task<string> Read(string path) => WebUtility.HtmlDecode(await owner.GetStringAsync(path));
        var name = "Discount-" + Guid.NewGuid().ToString("N");
        var productFields = ProductFields(name, "5000"); productFields["UnitCost"] = "3000";
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(owner, "/products/create", productFields)).StatusCode);
        var product = await FindProduct(name); var url = $"/simulations/discount?productId={product.Id}";
        Assert.Contains("/simulations/discount", await owner.GetStringAsync("/simulations"));
        await Submit(owner, "/culture", new() { ["culture"] = culture, ["returnUrl"] = url }, url);
        var fields = new Dictionary<string, string> { ["ProductId"] = product.Id.ToString(),
            ["DiscountPercentage"] = "10", ["CurrentSalesQuantity"] = "100", ["ExpectedSalesQuantity"] = "140",
            ["CampaignCost"] = "0", ["ComparisonPeriod"] = "Week", ["PreviewCurrentPrice"] = "5000", ["PreviewUnitCost"] = "3000",
            ["BusinessId"] = Guid.NewGuid().ToString(), ["CreatedByUserId"] = Guid.NewGuid().ToString(), ["Difference"] = "999999" };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/simulations/discount", new FormUrlEncodedContent(fields))).StatusCode);
        var preview = await Submit(owner, "/simulations/discount", fields, url);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var html = WebUtility.HtmlDecode(await preview.Content.ReadAsStringAsync());
        Assert.Contains("id=\"discount-result\"", html); Assert.Contains(Money(4500), html);
        Assert.Contains("134", html); Assert.Contains(Money(210000), html); Assert.Contains(Money(10000), html);
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AnyAsync(x => x.ProductId == product.Id));
        fields["DiscountPercentage"] = "0";
        var invalid = await Submit(owner, "/simulations/discount", fields, url);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var invalidHtml = WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync());
        Assert.Contains("data-valmsg-for=\"DiscountPercentage\"", invalidHtml);
        Assert.DoesNotContain("id=\"discount-result\"", invalidHtml);
        fields["DiscountPercentage"] = "10";
        var saved = await Submit(owner, "/simulations/discount/save", fields, url);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var savedUrl = saved.Headers.Location!.OriginalString; var id = Guid.Parse(savedUrl.Split('/').Last());
        var editUrl = savedUrl + "/discount/edit"; var saveUrl = savedUrl + "/discount/save";
        Assert.Contains(Money(210000), await Read(savedUrl));
        Assert.Contains(name, await owner.GetStringAsync("/simulations"));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var simulation = await db.PricingSimulations.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(product.BusinessId, simulation.BusinessId); Assert.NotEqual(Guid.Parse(fields["CreatedByUserId"]), simulation.CreatedByUserId);
            Assert.Equal(10000, simulation.Difference); Assert.Equal(134, simulation.DiscountSnapshot!.Result.MinimumUnits);
            Assert.Equal(0, simulation.DiscountSnapshot.Inputs.CampaignCost); Assert.Equal(SalesPeriod.Week, simulation.ComparisonPeriod);
        }
        productFields["SellingPrice"] = "6000"; productFields["UnitCost"] = "3500";
        await Submit(owner, $"/products/{product.Id}/edit", productFields);
        Assert.Contains(Money(210000), await Read(savedUrl));
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(owner, "/simulations/discount/save", fields, url)).StatusCode);
        fields["Version"] = "1"; fields["CampaignCost"] = "30000";
        Assert.Equal(HttpStatusCode.OK, (await Submit(owner, editUrl, fields)).StatusCode);
        // Previewing an edit does not write the proposed campaign cost.
        Assert.Contains(Money(210000), await Read(savedUrl));
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(owner, saveUrl, fields, editUrl)).StatusCode);
        Assert.Contains(Money(180000), await Read(savedUrl));
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(owner, saveUrl, fields, editUrl)).StatusCode);

        using var other = Browser(); await Login(other, await RegisterAndConfirm(other));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(savedUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(editUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Submit(other, saveUrl, fields, "/products")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Submit(other, "/simulations/discount/save", fields, "/products")).StatusCode);
        Assert.DoesNotContain(name, await other.GetStringAsync("/simulations"));

        // Existing JWT lifecycle endpoints operate on the same tenant-safe, versioned aggregate.
        using var api = Browser();
        var login = await api.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Password1!" });
        login.EnsureSuccessStatusCode(); var token = await login.Content.ReadFromJsonAsync<JsonElement>();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
        var apiUrl = $"/api/pricing-simulations/{id}";
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync(apiUrl)).StatusCode); // unchanged classic contract
        Assert.Equal(HttpStatusCode.Conflict, (await api.PutAsJsonAsync(apiUrl, new { ProposedPrice = 4500, CurrentSalesQuantity = 100, ExpectedSalesQuantity = 140, Version = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.PatchAsJsonAsync(apiUrl + "/archive", new { Version = 2 })).StatusCode);
        Assert.DoesNotContain(name, await owner.GetStringAsync("/simulations"));
        Assert.Contains(name, await owner.GetStringAsync("/simulations?includeArchived=true"));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.GetAsync(editUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.PatchAsJsonAsync(apiUrl + "/unarchive", new { Version = 3 })).StatusCode);
        using var delete = new HttpRequestMessage(HttpMethod.Delete, apiUrl) { Content = JsonContent.Create(new { Version = 4 }) };
        Assert.Equal(HttpStatusCode.OK, (await api.SendAsync(delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(savedUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.PatchAsJsonAsync(apiUrl + "/restore", new { Version = 5 })).StatusCode);
        Assert.Contains(Money(180000), await Read(savedUrl));
        using var finalScope = factory.Services.CreateScope();
        var stored = await finalScope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(5000, stored.CurrentPrice); Assert.Equal(3000, stored.UnitCost); Assert.Equal(6, stored.Version);
        // Two independently tracked copies prove the version check is in the SQL write, not just memory.
        using var firstScope = factory.Services.CreateScope(); using var secondScope = factory.Services.CreateScope();
        var firstRepo = new PricingSimulationRepository(firstScope.ServiceProvider.GetRequiredService<FinSightDbContext>());
        var secondRepo = new PricingSimulationRepository(secondScope.ServiceProvider.GetRequiredService<FinSightDbContext>());
        var first = (await firstRepo.GetByIdForUpdateAsync(id, product.BusinessId))!;
        var second = (await secondRepo.GetByIdForUpdateAsync(id, product.BusinessId))!;
        var calculator = new DiscountSimulationCalculator(new()); var now = factory.TimeProvider.GetUtcNow().UtcDateTime;
        first.UpdateDiscount(10, 100, 150, 0, SalesPeriod.Week, now, 6, calculator);
        second.UpdateDiscount(10, 100, 160, 0, SalesPeriod.Week, now, 6, calculator);
        await firstRepo.SaveChangesAsync();
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => secondRepo.SaveChangesAsync());
    }

    [Fact]
    public async Task Discount_UnknownInputsRemainNull_AndInactiveProductsCannotBePreviewed()
    {
        using var owner = Browser(); await Login(owner, await RegisterAndConfirm(owner));
        var name = "UnknownDiscount-" + Guid.NewGuid().ToString("N");
        var productFields = ProductFields(name, "5000"); productFields["UnitCost"] = "3000";
        await Submit(owner, "/products/create", productFields); var product = await FindProduct(name);
        var url = $"/simulations/discount?productId={product.Id}";
        var fields = new Dictionary<string, string> { ["ProductId"] = product.Id.ToString(), ["DiscountPercentage"] = "10",
            ["CurrentSalesQuantity"] = "100", ["ComparisonPeriod"] = "Month", ["PreviewCurrentPrice"] = "5000", ["PreviewUnitCost"] = "3000" };
        var result = await Submit(owner, "/simulations/discount", fields, url);
        var html = await result.Content.ReadAsStringAsync();
        Assert.Contains("Campaign cost not included", html); Assert.Contains("No expected sales were entered", html);
        Assert.Contains("134", html);
        await Submit(owner, "/simulations/discount/save", fields, url);
        using (var scope = factory.Services.CreateScope())
        {
            var record = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
            Assert.Null(record.ExpectedSalesQuantity); Assert.Null(record.ProposedContribution); Assert.Null(record.Recommendation);
            Assert.Null(record.DiscountSnapshot!.Inputs.CampaignCost); Assert.Null(record.DiscountSnapshot.Result.ScenarioContribution);
        }
        fields.Remove("CurrentSalesQuantity");
        Assert.Equal(HttpStatusCode.BadRequest, (await Submit(owner, "/simulations/discount", fields, url)).StatusCode);
        await Submit(owner, $"/products/{product.Id}/deactivate", [], "/products");
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Submit(owner, "/simulations/discount", fields, "/products")).StatusCode);
    }
}
