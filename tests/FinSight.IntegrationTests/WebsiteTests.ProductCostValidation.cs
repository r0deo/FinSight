using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FinSight.Api.Website.Models;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.IntegrationTests;

public partial class WebsiteTests
{
    [Theory]
    [InlineData("en", "")]
    [InlineData("ar-IQ", "")]
    [InlineData("en", "not a price")]
    [InlineData("ar-IQ", "not a price")]
    public async Task ProductCostEstimate_BlankProductFields_Calculates1400WithoutSaving(string culture, string sellingPrice)
    {
        var trace = new CostBindingTrace();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<MvcOptions>(options => options.Filters.Add(trace))));
        using var browser = app.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await Login(browser, await RegisterAndConfirm(browser));
        await Submit(browser, "/culture", new() { ["culture"] = culture, ["returnUrl"] = "/products/create" }, "/products/create");
        var fields = ProductCostFields($"Estimate-{Guid.NewGuid():N}");
        foreach (var field in new[] { "Name", "Category", "SellingPrice", "UnitCost" }) fields[field] = "";
        fields["SellingPrice"] = sellingPrice;
        fields["costAction"] = "calculate";
        fields["CostHelper.Ingredients[0].UsedQuantity"] = culture == "en" ? "1" : "١";
        fields["CostHelper.Ingredients[0].UsedUnit"] = "Kilogram";
        int before;
        using (var scope = app.Services.CreateScope()) before = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Products.CountAsync();
        using var logs = new ExceptionLog();
        app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().AddProvider(logs);
        var response = await Submit(browser, "/products/create", fields);
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"HTTP {(int)response.StatusCode}; binding trace: {JsonSerializer.Serialize(trace.Stages)}; empty summary item: {Regex.IsMatch(html, @"<li[^>]*>\s*</li>")}; logged exceptions: {logs.Exceptions.Count}");
        Assert.NotNull(trace.EstimatedCost);
        Assert.Equal(1400m, trace.EstimatedCost);
        Assert.Contains("data-product-cost-result", html);
        Assert.DoesNotContain("validation-summary-errors", html);
        Assert.DoesNotMatch(@"<li[^>]*>\s*</li>", html);
        Assert.Equal(sellingPrice, CostInput(html, "SellingPrice"));
        using (var scope = app.Services.CreateScope()) Assert.Equal(before, await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Products.CountAsync());
        Assert.Empty(logs.Exceptions);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar-IQ")]
    public async Task ProductCostEstimate_InvalidInputs_ShowLocalizedFieldErrorsAndPreserveDraft(string culture)
    {
        using var browser = Browser();
        await Login(browser, await RegisterAndConfirm(browser));
        await Submit(browser, "/culture", new() { ["culture"] = culture, ["returnUrl"] = "/products/create" }, "/products/create");
        var fields = ProductCostFields($"Invalid-cost-{Guid.NewGuid():N}");
        fields["costAction"] = "calculate";
        fields["SellingPrice"] = ""; fields["UnitCost"] = "";
        var cases = new[]
        {
            ("Ingredients[0].PackQuantity", "", "Enter this quantity."),
            ("Ingredients[0].PackQuantity", "not a number", "Enter a valid number without grouping separators."),
            ("Ingredients[0].PackQuantity", "0", "Quantities must be positive, at most 1,000,000, with no more than six decimal places."),
            ("Ingredients[0].PackPrice", "", "Enter the purchase-pack price."),
            ("Ingredients[0].PackPrice", "-1", "Costs must be non-negative, at most 999,999,999,999.99, with no more than two decimal places."),
            ("Ingredients[0].Name", "", "Each ingredient needs a name of at most 100 characters."),
            ("Ingredients[0].UsedUnit", "Litre", "Use compatible units: mass with mass, volume with volume, or items with items."),
            ("Ingredients[0].UsedUnit", "invalid", "Choose a supported ingredient unit."),
            ("UsableOutput", "", "Enter the usable batch output."),
            ("UsableOutput", "0", "Quantities must be positive, at most 1,000,000, with no more than six decimal places."),
            ("PackagingPerUnit", "-1", "Costs must be non-negative, at most 999,999,999,999.99, with no more than two decimal places.")
        };
        var resources = new System.Resources.ResourceManager("FinSight.Api.Resources.Website.Localization.RebazText", typeof(FinSight.Api.Website.Localization.RebazText).Assembly);
        foreach (var (path, value, message) in cases)
        {
            var key = "CostHelper." + path;
            var invalid = new Dictionary<string, string>(fields) { [key] = value };
            var response = await Submit(browser, "/products/create", invalid);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var expected = resources.GetString(message, System.Globalization.CultureInfo.GetCultureInfo(culture));
            Assert.False(string.IsNullOrWhiteSpace(expected));
            Assert.Equal(expected, FieldError(html, key));
            var summary = Regex.Match(html, "<div[^>]*class=\"[^\"]*validation-summary-errors[^\"]*\"[^>]*>([\\s\\S]*?)</div>").Value;
            Assert.Single(Regex.Matches(summary, "<li>"));
            if (culture != "en") Assert.NotEqual(message, expected);
            Assert.DoesNotMatch(@"<li[^>]*>\s*</li>", html);
            Assert.Equal(fields["Name"], CostInput(html, "Name"));
            Assert.Equal("0", CostInput(html, "CostHelper.OtherVariableCostPerUnit"));
            if (!path.EndsWith("Unit")) Assert.Equal(value, CostInput(html, key));
            var packSelect = Regex.Match(html, "<select[^>]*name=\"CostHelper.Ingredients\\[0\\].PackUnit\"[\\s\\S]*?</select>").Value;
            Assert.Matches("<option(?=[^>]*selected)(?=[^>]*value=\"Kilogram\")[^>]*>", packSelect);
        }
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Products.AnyAsync(x => x.Name == fields["Name"]));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar-IQ")]
    public async Task ProductSave_AfterEstimate_StillRequiresCompleteValidProduct(string culture)
    {
        using var browser = Browser();
        await Login(browser, await RegisterAndConfirm(browser));
        await Submit(browser, "/culture", new() { ["culture"] = culture, ["returnUrl"] = "/products/create" }, "/products/create");
        var fields = ProductCostFields($"Save-validation-{Guid.NewGuid():N}");
        var productName = fields["Name"];
        foreach (var key in new[] { "Name", "Category", "SellingPrice", "UnitCost" }) fields[key] = "";
        fields["costAction"] = "calculate";
        var preview = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        fields["CostPreviewFingerprint"] = CostInput(await preview.Content.ReadAsStringAsync(), "CostPreviewFingerprint");
        fields["costAction"] = "use";
        var selected = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        fields.Remove("costAction");
        fields["UseCostHelper"] = "true"; fields["UnitCost"] = CostInput(await selected.Content.ReadAsStringAsync(), "UnitCost");
        Assert.Equal("1400", fields["UnitCost"]);
        var rejected = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var html = await rejected.Content.ReadAsStringAsync();
        foreach (var key in new[] { "Name", "Category", "SellingPrice" }) Assert.False(string.IsNullOrWhiteSpace(FieldError(html, key)));
        fields["Name"] = productName; fields["Category"] = "Meals"; fields["SellingPrice"] = "-1";
        rejected = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(FieldError(await rejected.Content.ReadAsStringAsync(), "SellingPrice")));
        fields["SellingPrice"] = "5000"; fields["UseCostHelper"] = "false"; fields["UnitCost"] = "";
        rejected = await Submit(browser, "/products/create", fields);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(FieldError(await rejected.Content.ReadAsStringAsync(), "UnitCost")));
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Products.AnyAsync(x => x.Name == productName));
    }

    private static string FieldError(string html, string field) => WebUtility.HtmlDecode(Regex.Replace(
        Regex.Match(html, "<span[^>]*data-valmsg-for=\"" + Regex.Escape(field) + "\"[^>]*>([\\s\\S]*?)</span>").Groups[1].Value,
        "<[^>]+>", "")).Trim();

    private sealed class CostBindingTrace : IActionFilter
    {
        public List<object> Stages { get; } = [];
        public decimal? EstimatedCost { get; private set; }
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionArguments.ContainsKey("costAction") && context.ActionArguments["costAction"] as string == "calculate")
                Capture("before", context.ModelState);
        }
        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Result is ViewResult { ViewData.Model: ProductFormViewModel model } && model.HelperOpen)
            { EstimatedCost = model.CostPreview?.Result.EstimatedUnitCost; Capture("after", context.ModelState); }
        }
        private void Capture(string stage, ModelStateDictionary state) => Stages.Add(new
        {
            Stage = stage, state.IsValid, state.ErrorCount,
            Fields = state.Where(x => x.Key is "Name" or "Category" or "UnitCost" or "SellingPrice" || x.Key.StartsWith("CostHelper.", StringComparison.Ordinal))
                .Select(x => new { x.Key, x.Value!.AttemptedValue, ValidationState = x.Value.ValidationState.ToString(),
                    Errors = x.Value.Errors.Select(e => e.ErrorMessage).ToArray() }).ToArray()
        });
    }
}
