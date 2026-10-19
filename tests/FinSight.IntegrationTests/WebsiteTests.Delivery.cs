using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Globalization;
using FinSight.Domain.Models;
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
    private static Dictionary<string, string> DeliveryFields(Guid productId) => new()
    {
        ["ProductId"] = productId.ToString(), ["UnitsPerOrder"] = "1", ["DeliveryUnitPrice"] = "6000",
        ["CommissionPercentage"] = "25", ["PackagingPerOrder"] = "500", ["OtherChargesPerOrder"] = "0",
        ["FixedPeriodCost"] = "20000", ["ComparisonPeriod"] = "Month", ["CurrentDirectOrders"] = "100",
        ["ExpectedDeliveryOrders"] = "50", ["TransferredOrders"] = "10", ["PreviewCurrentPrice"] = "6000", ["PreviewUnitCost"] = "3000"
    };

    [Theory]
    [InlineData("en")]
    [InlineData("ar-IQ")]
    [InlineData("ku-Arab-IQ")]
    public async Task Delivery_AuthenticatedPreviewSaveEditHistoryAndLifecycle_PreserveTenantAndSnapshot(string culture)
    {
        string Money(decimal amount) => amount.ToString("N2", CultureInfo.GetCultureInfo(culture));
        using var owner = Browser(); var email = await RegisterAndConfirm(owner); await Login(owner, email);
        async Task<string> Read(string path) => WebUtility.HtmlDecode(await owner.GetStringAsync(path));
        var name = "Delivery-" + Guid.NewGuid().ToString("N");
        var productFields = ProductFields(name, "6000"); productFields["UnitCost"] = "3000";
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(owner, "/products/create", productFields)).StatusCode);
        var product = await FindProduct(name); var url = $"/simulations/delivery?productId={product.Id}";
        Assert.Contains("/simulations/delivery", await owner.GetStringAsync("/simulations/preview"));
        await Submit(owner, "/culture", new() { ["culture"] = culture, ["returnUrl"] = url }, url);
        var fields = DeliveryFields(product.Id);
        fields["BusinessId"] = Guid.NewGuid().ToString(); fields["CreatedByUserId"] = Guid.NewGuid().ToString(); fields["Difference"] = "999999";
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/simulations/delivery", new FormUrlEncodedContent(fields))).StatusCode);
        if (culture != "en") fields["CommissionPercentage"] = "٢٥";
        var preview = await Submit(owner, "/simulations/delivery", fields, url);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var html = WebUtility.HtmlDecode(await preview.Content.ReadAsStringAsync());
        Assert.Contains("id=\"delivery-result\"", html); Assert.Contains(Money(1500), html);
        Assert.Contains(Money(1000), html); Assert.Contains(Money(300000), html);
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AnyAsync(x => x.ProductId == product.Id));
        fields["TransferredOrders"] = "51";
        var invalid = await Submit(owner, "/simulations/delivery", fields, url);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var invalidHtml = WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync());
        Assert.Contains("data-valmsg-for=\"TransferredOrders\"", invalidHtml);
        Assert.DoesNotContain("id=\"delivery-result\"", invalidHtml);
        fields["TransferredOrders"] = "10";
        var saved = await Submit(owner, "/simulations/delivery/save", fields, url);
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var savedUrl = saved.Headers.Location!.OriginalString; var id = Guid.Parse(savedUrl.Split('/').Last());
        var editUrl = savedUrl + "/delivery/edit"; var saveUrl = savedUrl + "/delivery/save";
        Assert.Contains(Money(300000), await Read(savedUrl)); Assert.Contains(name, await owner.GetStringAsync("/simulations"));
        using (var scope = factory.Services.CreateScope())
        {
            var simulation = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal(product.BusinessId, simulation.BusinessId); Assert.NotEqual(Guid.Parse(fields["CreatedByUserId"]), simulation.CreatedByUserId);
            Assert.Equal(0, simulation.Difference); Assert.Equal(90, simulation.DeliverySnapshot!.Result.RemainingDirectOrders);
            Assert.Equal("ProductSubtotal", simulation.DeliverySnapshot.CommissionBasis); Assert.True(simulation.DeliverySnapshot.DirectDemandOtherwiseUnchanged);
            Assert.Equal("IQD", simulation.CurrencySnapshot); Assert.Equal(SalesPeriod.Month, simulation.ComparisonPeriod);
        }
        productFields["SellingPrice"] = "7000"; productFields["UnitCost"] = "3500";
        await Submit(owner, $"/products/{product.Id}/edit", productFields);
        Assert.Contains(Money(300000), await Read(savedUrl));
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(owner, "/simulations/delivery/save", fields, url)).StatusCode);
        fields["Version"] = "1"; fields["TransferredOrders"] = "0";
        var editedPreview = await Submit(owner, editUrl, fields);
        Assert.Equal(HttpStatusCode.OK, editedPreview.StatusCode);
        Assert.Contains(Money(330000), WebUtility.HtmlDecode(await editedPreview.Content.ReadAsStringAsync()));
        Assert.Contains(Money(300000), await Read(savedUrl)); // edit preview did not write
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(owner, saveUrl, fields, editUrl)).StatusCode);
        Assert.Contains(Money(330000), await Read(savedUrl));
        Assert.Equal(HttpStatusCode.Conflict, (await Submit(owner, saveUrl, fields, editUrl)).StatusCode);

        using var other = Browser(); await Login(other, await RegisterAndConfirm(other));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(savedUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(editUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Submit(other, saveUrl, fields, "/products")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Submit(other, "/simulations/delivery/save", fields, "/products")).StatusCode);
        Assert.DoesNotContain(name, await other.GetStringAsync("/simulations"));

        using var api = Browser();
        var login = await api.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Password1!" });
        login.EnsureSuccessStatusCode(); var token = await login.Content.ReadFromJsonAsync<JsonElement>();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
        var apiUrl = $"/api/pricing-simulations/{id}";
        Assert.Equal(HttpStatusCode.NotFound, (await api.GetAsync(apiUrl)).StatusCode); // classic contract unchanged
        Assert.Equal(HttpStatusCode.Conflict, (await api.PutAsJsonAsync(apiUrl, new { ProposedPrice = 4500, CurrentSalesQuantity = 100, ExpectedSalesQuantity = 140, Version = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.PatchAsJsonAsync(apiUrl + "/archive", new { Version = 2 })).StatusCode);
        Assert.DoesNotContain(name, await owner.GetStringAsync("/simulations")); Assert.Contains(name, await owner.GetStringAsync("/simulations?includeArchived=true"));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.GetAsync(editUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.PatchAsJsonAsync(apiUrl + "/unarchive", new { Version = 3 })).StatusCode);
        using var delete = new HttpRequestMessage(HttpMethod.Delete, apiUrl) { Content = JsonContent.Create(new { Version = 4 }) };
        Assert.Equal(HttpStatusCode.OK, (await api.SendAsync(delete)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(savedUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.PatchAsJsonAsync(apiUrl + "/restore", new { Version = 5 })).StatusCode);
        Assert.Contains(Money(330000), await Read(savedUrl));
        await Submit(owner, $"/products/{product.Id}/deactivate", [], "/products");
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(url)).StatusCode);
        Assert.Contains(Money(330000), await Read(savedUrl)); // history remains after soft-delete
        using var firstScope = factory.Services.CreateScope(); using var secondScope = factory.Services.CreateScope();
        var firstRepo = new PricingSimulationRepository(firstScope.ServiceProvider.GetRequiredService<FinSightDbContext>());
        var secondRepo = new PricingSimulationRepository(secondScope.ServiceProvider.GetRequiredService<FinSightDbContext>());
        var first = (await firstRepo.GetByIdForUpdateAsync(id, product.BusinessId))!;
        var second = (await secondRepo.GetByIdForUpdateAsync(id, product.BusinessId))!;
        Assert.Equal(6000, first.CurrentPrice); Assert.Equal(3000, first.UnitCost); Assert.Equal(6, first.Version);
        var calculator = new DeliverySimulationCalculator(new()); var now = factory.TimeProvider.GetUtcNow().UtcDateTime;
        first.UpdateDelivery(first.DeliverySnapshot!.Inputs with { TransferredOrders = 5 }, now, 6, calculator);
        second.UpdateDelivery(second.DeliverySnapshot!.Inputs with { TransferredOrders = 15 }, now, 6, calculator);
        await firstRepo.SaveChangesAsync();
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => secondRepo.SaveChangesAsync());
    }

    [Fact]
    public async Task Delivery_ExplicitChargeFieldsRequired_UnknownCountsAndFixedCostsSurviveSave()
    {
        using var owner = Browser(); await Login(owner, await RegisterAndConfirm(owner));
        var name = "UnknownDelivery-" + Guid.NewGuid().ToString("N");
        var productFields = ProductFields(name, "6000"); productFields["UnitCost"] = "3000";
        await Submit(owner, "/products/create", productFields); var product = await FindProduct(name);
        var url = $"/simulations/delivery?productId={product.Id}"; var fields = DeliveryFields(product.Id);
        foreach (var field in new[] { "UnitsPerOrder", "DeliveryUnitPrice", "CommissionPercentage", "PackagingPerOrder", "OtherChargesPerOrder" })
        {
            var missing = new Dictionary<string, string>(fields); missing.Remove(field);
            var response = await Submit(owner, "/simulations/delivery", missing, url);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("is required", await response.Content.ReadAsStringAsync());
        }
        fields.Remove("TransferredOrders"); fields.Remove("FixedPeriodCost");
        var result = await Submit(owner, "/simulations/delivery", fields, url);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var html = WebUtility.HtmlDecode(await result.Content.ReadAsStringAsync());
        Assert.Contains("Additional fixed costs are not included", html); Assert.Contains("No overall better/worse result", html);
        Assert.DoesNotContain("data-delivery-scenario", html);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(owner, "/simulations/delivery/save", fields, url)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var record = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AsNoTracking().SingleAsync(x => x.ProductId == product.Id);
        Assert.Null(record.ProposedContribution); Assert.Null(record.Recommendation); Assert.Null(record.Difference);
        Assert.Null(record.DeliverySnapshot!.Inputs.FixedPeriodCost); Assert.Null(record.DeliverySnapshot.Inputs.TransferredOrders);
        Assert.Null(record.DeliverySnapshot.Result.BaselineContribution); Assert.Equal(1000, record.DeliverySnapshot.Result.DeliveryContributionPerOrder);
    }
}
