using System.Globalization;
using System.Security.Claims;
using FinSight.Api.Website.Models;
using FinSight.Application.Features.Products;
using FinSight.Application.Features.Products.Queries.GetProducts;
using FinSight.Application.Features.PricingSimulations.Queries.GetSimulations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FinSight.IntegrationTests;

// Presentation-only fixtures render the real Razor views and run real browser JS.
// They do not replace the PostgreSQL tests for authorization, calculation or saving.
public sealed partial class DecisionWorkspacePresentationTests(PublicPagesFactory factory) : IClassFixture<PublicPagesFactory>
{
    [Theory]
    [InlineData("en", 1440)]
    [InlineData("en", 390)]
    [InlineData("ar-IQ", 1440)]
    [InlineData("ar-IQ", 390)]
    [InlineData("ku-Arab-IQ", 1440)]
    [InlineData("ku-Arab-IQ", 390)]
    public async Task ProductFirstViews_TabsSelectionDraftGuardAndRtl(string culture, int width)
    {
        var id = Guid.NewGuid();
        const string name = "Price & قهوة <Owner>";
        var product = new ProductResponse(id, name, "Coffee", null, 4000, 1000, true);
        var products = new GetProductsResponse([product], 1, 20, 21, 2);
        var picker = await Render("Shared/DecisionProductPicker", "SimulationPages", "/simulations/preview",
            new DecisionProductPickerViewModel("pricing", products), culture);
        var pricing = await Render("SimulationPages/Preview", "SimulationPages", "/simulations/preview",
            new PreviewSimulationViewModel { ProductId = id, ProductName = name, Currency = "IQD", CurrentPrice = 4000,
                UnitCost = 1000, ProposedPrice = 4000, UnknownExpectedSales = true }, culture);
        var discount = await Render("DiscountPages/Preview", "DiscountPages", "/simulations/discount",
            new DiscountViewModel { ProductId = id, ProductName = name, Currency = "IQD", CurrentPrice = 4000, UnitCost = 1000 }, culture);
        var delivery = await Render("DeliveryPages/Preview", "DeliveryPages", "/simulations/delivery",
            new DeliveryViewModel { ProductId = id, ProductName = name, Currency = "IQD", CurrentPrice = 4000,
                UnitCost = 1000, UnitsPerOrder = 1, DeliveryUnitPrice = 4000 }, culture);
        var whatIf = await Render("WhatIfPages/Index", "WhatIfPages", "/what-if",
            new WhatIfFormViewModel { ProductId = id, ProductName = name, Currency = "IQD", CurrentPrice = 4000, CurrentUnitCost = 1000 }, culture);
        var history = await Render("SavedSimulationPages/Index", "SavedSimulationPages", "/simulations",
            new SavedSimulationListViewModel(new GetSimulationsResponse([], 1, 20, 0, 0), false), culture);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new()
        { BaseURL = factory.Origin, IgnoreHTTPSErrors = true, ViewportSize = new() { Width = width, Height = 900 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.RouteAsync("**/*", async route =>
        {
            var uri = new Uri(route.Request.Url);
            string? html = uri.AbsolutePath switch
            {
                "/simulations/preview" => uri.Query.Contains("productId=") ? pricing : picker,
                "/simulations/discount" => discount,
                "/simulations/delivery" => delivery,
                "/what-if" => whatIf,
                "/simulations" => history,
                _ => null
            };
            if (html is null) await route.ContinueAsync();
            else await route.FulfillAsync(new() { Status = 200, ContentType = "text/html; charset=utf-8", Body = html });
        });
        await page.GotoAsync("/simulations/preview");
        await Expect(page.Locator(".simulation-destination")).ToHaveCountAsync(3);
        await Expect(page.Locator(".simulation-destination[aria-current=page]")).ToHaveCountAsync(1);
        await Expect(page.Locator(".decision-selected")).ToHaveCountAsync(0);
        await Expect(page.Locator(".decision-card[aria-current]")).ToHaveCountAsync(0);
        await Expect(page.Locator("[data-start-simulation]")).ToBeDisabledAsync();
        Assert.False(string.IsNullOrWhiteSpace(await page.Locator("#launcher-status").InnerTextAsync()));
        await Expect(page.Locator(".decision-product-option bdi")).ToHaveTextAsync(name);
        await Expect(page.Locator(".pagination a")).ToHaveAttributeAsync("href", "/simulations/preview?page=2");
        var pickerBox = (await page.Locator(".decision-product-picker").BoundingBoxAsync())!;
        var cardsBox = (await page.Locator(".decision-cards").BoundingBoxAsync())!;
        Assert.True(pickerBox.Y < cardsBox.Y);
        await CheckLayout(page, culture, width, "picker");
        await page.Locator($"input[name=productId][value='{id}']").CheckAsync();
        await Expect(page.Locator("[data-start-simulation]")).ToBeDisabledAsync();
        await Expect(page.Locator("[data-decision-form], [data-discount-form], [data-delivery-form], dialog")).ToHaveCountAsync(0);
        await page.Locator("input[name=decision][value=pricing]").CheckAsync();
        await Expect(page.Locator("[data-start-simulation]")).ToBeEnabledAsync();
        await CheckLayout(page, culture, width, "ready-to-start");
        foreach (var (decision, route, form) in new[]
        {
            ("pricing", "/simulations/preview", "data-decision-form"),
            ("discount", "/simulations/discount", "data-discount-form"),
            ("delivery", "/simulations/delivery", "data-delivery-form")
        })
        {
            await page.GotoAsync("/simulations/preview");
            await Expect(page.Locator($"input[name=productId][value='{id}']")).ToBeCheckedAsync();
            await page.Locator($"input[name=decision][value='{decision}']").CheckAsync();
            await page.Locator("[data-start-simulation]").ClickAsync();
            await page.WaitForURLAsync($"**{route}?productId={id}");
            await Expect(page.Locator(".decision-product-summary bdi")).ToHaveTextAsync(name);
            await Expect(page.Locator(".decision-card[aria-current=page]")).ToHaveCountAsync(1);
            await Expect(page.Locator(".decision-card[aria-current=page]")).ToHaveAttributeAsync("data-decision-link", decision);
            await Expect(page.Locator($"form[{form}]")).ToHaveCountAsync(1);
            await Expect(page.Locator("dialog, [data-result-open]")).ToHaveCountAsync(0);
            var neutral = page.Locator("a.decision-card:not([aria-current])").First;
            Assert.Equal("#F4F2EB", await neutral.EvaluateAsync<string>("e => getComputedStyle(document.documentElement).getPropertyValue('--text').trim()"));
            Assert.Equal("rgb(244, 242, 235)", await neutral.EvaluateAsync<string>("e => getComputedStyle(e).color"));
            await neutral.HoverAsync();
            Assert.Equal("rgb(48, 57, 68)", await neutral.EvaluateAsync<string>("e => getComputedStyle(e).borderTopColor"));
            await CheckLayout(page, culture, width, decision);
        }
        // Bookmarks created by the previous UI still open the existing form directly.
        await page.GotoAsync($"/simulations/preview?productId={id}&chooseDecision=true");
        await Expect(page.Locator("[data-decision-form]")).ToHaveCountAsync(1);
        await page.GotoAsync($"/simulations/delivery?productId={id}");
        await page.Locator("#CommissionPercentage").FillAsync("25");
        EventHandler<IDialog> dismiss = async (_, dialog) => await dialog.DismissAsync();
        page.Dialog += dismiss;
        await page.Locator("[data-product-selection] a").ClickAsync();
        await Expect(page.Locator("#CommissionPercentage")).ToHaveValueAsync("25");
        Assert.Contains("/simulations/delivery?productId=", page.Url);
        page.Dialog -= dismiss;
        EventHandler<IDialog> accept = async (_, dialog) => await dialog.AcceptAsync();
        page.Dialog += accept;
        await page.Locator(".simulation-destination[data-decision-link='what-if']").ClickAsync();
        await page.WaitForURLAsync($"**/what-if?productId={id}");
        page.Dialog -= accept;
        await Expect(page.Locator(".decision-step, .decision-cards")).ToHaveCountAsync(0);
        await Expect(page.Locator(".simulation-destination[aria-current=page]")).ToHaveAttributeAsync("data-decision-link", "what-if");
        await page.Locator(".simulation-destination[data-decision-link='history']").ClickAsync();
        await page.WaitForURLAsync("**/simulations");
        await Expect(page.Locator(".decision-step, .decision-cards")).ToHaveCountAsync(0);
        await Expect(page.Locator(".simulation-destination[aria-current=page]")).ToHaveAttributeAsync("data-decision-link", "history");
        await CheckLayout(page, culture, width, "history");
        Assert.Empty(errors);
    }

    private async Task<string> Render(string view, string controller, string path, object model, string culture, bool owner = false)
    {
        using var scope = factory.Services.CreateScope();
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var previousContext = accessor.HttpContext;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
            var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            http.Request.Scheme = "https"; http.Request.Host = new HostString(new Uri(factory.Origin).Authority);
            http.Request.Path = path; http.Request.Method = "GET";
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Presentation fixture"),
                new Claim(ClaimTypes.Role, owner ? "Owner" : "Manager")], "fixture"));
            accessor.HttpContext = http;
            var route = new RouteData();
            route.Values["controller"] = controller; route.Values["action"] = "Preview";
            http.Request.RouteValues = route.Values;
            // Select the endpoint-routing URL helper used by the real application.
            http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "Razor presentation fixture"));
            var action = new ActionContext(http, route, new ControllerActionDescriptor { ControllerName = controller, ActionName = "Preview" });
            var engine = scope.ServiceProvider.GetRequiredService<ICompositeViewEngine>();
            var result = engine.GetView(null, $"/Views/{view}.cshtml", true);
            Assert.True(result.Success, string.Join(", ", result.SearchedLocations ?? []));
            var viewData = new ViewDataDictionary(scope.ServiceProvider.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
            var tempData = new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
            using var writer = new StringWriter();
            await result.View.RenderAsync(new ViewContext(action, result.View, viewData, tempData, writer, new HtmlHelperOptions()));
            return writer.ToString();
        }
        finally { accessor.HttpContext = previousContext; CultureInfo.CurrentCulture = previous; CultureInfo.CurrentUICulture = previousUi; }
    }

    private static async Task CheckLayout(IPage page, string culture, int width, string screen)
    {
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", culture);
        await Expect(page.Locator("html")).ToHaveAttributeAsync("dir", culture == "en" ? "ltr" : "rtl");
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"));
        var folder = Path.Combine(AppContext.BaseDirectory, "TestResults", "browser");
        Directory.CreateDirectory(folder);
        await page.ScreenshotAsync(new() { Path = Path.Combine(folder, $"{culture}-{width}-decision-workspace-{screen}.png"), FullPage = true });
    }
}
