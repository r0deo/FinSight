using System.Globalization;
using FinSight.Api.Website.Models;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using FinSight.Domain.Services;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FinSight.IntegrationTests;

public sealed partial class DecisionWorkspacePresentationTests
{
    [Theory]
    [InlineData("en", 1440)] [InlineData("en", 390)]
    [InlineData("ar-IQ", 1440)] [InlineData("ar-IQ", 390)]
    [InlineData("ku-Arab-IQ", 1440)] [InlineData("ku-Arab-IQ", 390)]
    public async Task Results_LeadWithImpactAndKeepDetailsOptional(string culture, int width)
    {
        var id = Guid.NewGuid();
        var pricing = new PreviewSimulationViewModel { ProductId = id, ProductName = "Owner coffee قهوة", Currency = "IQD",
            CurrentPrice = 4000, UnitCost = 1000, ProposedPrice = 4500, CurrentSalesQuantity = 100,
            ExpectedSalesQuantity = 100, ComparisonPeriod = SalesPeriod.Week,
            Result = new PricingDecisionCalculator(new()).Calculate(4000, 4500, 1000, 100, 100, 10) };
        var discount = new DiscountViewModel { ProductId = id, ProductName = pricing.ProductName, Currency = "IQD",
            CurrentPrice = 5000, UnitCost = 3000, DiscountPercentage = 10, CurrentSalesQuantity = 100, ExpectedSalesQuantity = 140,
            ComparisonPeriod = SalesPeriod.Week, Result = new DiscountSimulationCalculator(new()).Calculate(new(5000, 3000, 10, 100, 140, null)) };
        var delivery = new DeliveryViewModel { ProductId = id, ProductName = pricing.ProductName, Currency = "IQD",
            CurrentPrice = 6000, UnitCost = 3000, UnitsPerOrder = 1, DeliveryUnitPrice = 6000, CommissionPercentage = 25,
            PackagingPerOrder = 500, OtherChargesPerOrder = 0,
            Result = new DeliverySimulationCalculator(new()).Calculate(6000, 3000, new(1, 6000, 25, 500, 0, null, SalesPeriod.Week, null, null, null)) };
        var input = new WhatIfInputs(4000, 1000, 100, CostShockInputMode.Percentage, 20, null, 10, null, null, null, null);
        var whatIf = new WhatIfFormViewModel { ProductId = id, ProductName = pricing.ProductName, Currency = "IQD",
            CurrentPrice = 4000, CurrentUnitCost = 1000, CurrentQuantity = 100, CostIncreasePercent = 20,
            CostInputMode = CostShockInputMode.Percentage, SalesDeclinePercent = 10, Period = SalesPeriod.Week,
            Result = new WhatIfCalculator(new()).Calculate(input) };
        var html = new Dictionary<string, string> {
            ["/simulations/preview"] = await Render("SimulationPages/Preview", "SimulationPages", "/simulations/preview", pricing, culture),
            ["/simulations/discount"] = await Render("DiscountPages/Preview", "DiscountPages", "/simulations/discount", discount, culture),
            ["/simulations/delivery"] = await Render("DeliveryPages/Preview", "DeliveryPages", "/simulations/delivery", delivery, culture),
            ["/what-if"] = await Render("WhatIfPages/Index", "WhatIfPages", "/what-if", whatIf, culture)
        };
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { BaseURL = factory.Origin, IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = width, Height = 900 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.RouteAsync("**/*", async route => {
            if (html.TryGetValue(new Uri(route.Request.Url).AbsolutePath, out var body))
                await route.FulfillAsync(new() { Status = 200, ContentType = "text/html; charset=utf-8", Body = body });
            else await route.ContinueAsync();
        });
        var money = CultureInfo.GetCultureInfo(culture);
        foreach (var (path, amount) in new[] { ("/simulations/preview", 50000m), ("/simulations/discount", 10000m), ("/simulations/delivery", 1000m), ("/what-if", 48000m) })
        {
            await page.GotoAsync(path);
            var dialog = page.Locator("dialog[open]");
            await Expect(dialog).ToBeVisibleAsync();
            await Expect(dialog.Locator("[data-result-close]")).ToBeFocusedAsync();
            var answer = path.EndsWith("delivery") ? dialog.Locator("[data-result-answer]") : dialog.Locator("[data-period-impact]");
            await Expect(answer).ToContainTextAsync(amount.ToString("N2", money));
            await Expect(answer).ToContainTextAsync("IQD");
            await Expect(answer).ToBeVisibleAsync();
            if (path.EndsWith("discount")) await Expect(dialog.Locator(".decision-answer")).ToContainTextAsync("34");
            if (path.EndsWith("delivery")) await Expect(dialog.Locator("[data-delivery-scenario]")).ToHaveCountAsync(0);
            if (culture != "en") Assert.DoesNotContain("Under your assumption", await answer.InnerTextAsync());
            var details = dialog.Locator("details.result-details").First;
            Assert.Null(await details.GetAttributeAsync("open"));
            await CheckLayout(page, culture, width, path.Split('/').Last() + "-concise-result");
            await details.Locator(":scope > summary").ClickAsync();
            Assert.NotNull(await details.GetAttributeAsync("open"));
            await details.Locator(":scope > summary").ClickAsync();
            await page.Keyboard.PressAsync("Escape");
            await Expect(dialog).ToHaveCountAsync(0);
            var period = page.Locator("select[name=ComparisonPeriod], select[name=Period]");
            await period.SelectOptionAsync("Day");
            var label = await period.Locator("option:checked").InnerTextAsync();
            var quantities = page.Locator("#CurrentSalesQuantity-example, #CurrentQuantity-example, #CurrentDirectOrders-example");
            await Expect(quantities.First).ToContainTextAsync(label);
            await Expect(page.Locator("[data-result-open]")).ToBeHiddenAsync();
            var optional = page.Locator("[data-optional-inputs]");
            Assert.Null(await optional.GetAttributeAsync("open"));
            await optional.Locator(":scope > summary").ClickAsync();
            await Expect(page.Locator("#LinkCosts")).Not.ToBeCheckedAsync();
            if (path.EndsWith("preview"))
            {
                await page.Locator("#DeclinePercentage").FillAsync("");
                await optional.Locator(":scope > summary").ClickAsync();
                await page.Locator("[data-decision-form] button[type=submit]").ClickAsync();
                await Expect(page.Locator("#DeclinePercentage")).ToBeFocusedAsync();
                Assert.NotNull(await optional.GetAttributeAsync("open"));
            }
        }
        Assert.Empty(errors);
    }
}
