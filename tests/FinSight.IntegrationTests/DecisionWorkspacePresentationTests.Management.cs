using System.Globalization;
using FinSight.Api.Website.Models;
using FinSight.Application.Features.BusinessCosts;
using FinSight.Application.Features.Products;
using FinSight.Application.Features.Products.Queries.GetProducts;
using FinSight.Domain.Enums;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FinSight.IntegrationTests;

public sealed partial class DecisionWorkspacePresentationTests
{
    [Theory]
    [InlineData("en", 1440)]
    [InlineData("en", 390)]
    [InlineData("ar-IQ", 1440)]
    [InlineData("ar-IQ", 390)]
    [InlineData("ku-Arab-IQ", 1440)]
    [InlineData("ku-Arab-IQ", 390)]
    public async Task ProductsAndMonthlyCosts_PreserveActionsNamesAndFinancialBoundaries(string culture, int width)
    {
        var active = new ProductResponse(Guid.NewGuid(), "Coffee & قهوة <Owner>", "Owner category", null, 4000, 1000, true);
        var inactive = active with { Id = Guid.NewGuid(), Name = "Inactive owner name", IsActive = false };
        var productHtml = await Render("ProductPages/Index", "ProductPages", "/products",
            new ProductListViewModel(new GetProductsResponse([active, inactive], 1, 20, 21, 2), true), culture, owner: true);
        var month = new DateOnly(2026, 10, 1);
        var rent = new BusinessCostResponse(Guid.NewGuid(), "Rent & کرێ <Owner>", BusinessCostCategory.Rent, 125,
            month, CostCertainty.Actual, UnitCostInclusion.No, "Owner's note & تێبینی", 3);
        var estimate = rent with { Id = Guid.NewGuid(), Name = "Generator estimate", Category = BusinessCostCategory.Energy, Amount = 25, Certainty = CostCertainty.Estimated };
        var inventory = rent with { Id = Guid.NewGuid(), Name = "Ingredient purchases", Category = BusinessCostCategory.Inventory, Amount = 900 };
        var summary = new BusinessCostSummary(month, [rent, estimate, inventory]) { ReviewVersion = 7 };
        var costs = new BusinessCostsViewModel(summary, "IQD");
        var costHtml = await Render("BusinessCostPages/Index", "BusinessCostPages", "/business-costs", costs, culture, owner: true);
        var emptyHtml = await Render("BusinessCostPages/Index", "BusinessCostPages", "/business-costs",
            new BusinessCostsViewModel(new BusinessCostSummary(month.AddMonths(1), []), "IQD"), culture, owner: true);
        var managerHtml = await Render("BusinessCostPages/Index", "BusinessCostPages", "/business-costs", costs, culture);
        var createHtml = await Render("BusinessCostPages/Form", "BusinessCostPages", "/business-costs/create",
            new BusinessCostForm { Month = "2026-10" }, culture, owner: true);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new()
        { BaseURL = factory.Origin, IgnoreHTTPSErrors = true, ViewportSize = new() { Width = width, Height = 900 } });
        var page = await context.NewPageAsync();
        var mutations = 0;
        await page.RouteAsync("**/*", async route =>
        {
            var uri = new Uri(route.Request.Url);
            if (route.Request.Method == "POST")
            {
                mutations++;
                await route.AbortAsync();
                return;
            }
            var html = uri.AbsolutePath switch
            {
                "/products" => productHtml,
                "/business-costs/create" => createHtml,
                "/business-costs" => uri.Query.Contains("2026-11") ? emptyHtml : uri.Query.Contains("manager=true") ? managerHtml : costHtml,
                _ => null
            };
            if (html is null) await route.ContinueAsync();
            else await route.FulfillAsync(new() { Status = 200, ContentType = "text/html; charset=utf-8", Body = html });
        });
        await page.GotoAsync("/products");
        await Expect(page.Locator("tbody a[href*='/simulations']")).ToHaveCountAsync(0);
        await Expect(page.Locator("tbody tr").First.Locator(".row-actions a")).ToHaveAttributeAsync("href", $"/products/{active.Id}/edit");
        await Expect(page.Locator("tbody tr").First.Locator("form")).ToHaveAttributeAsync("action", $"/products/{active.Id}/deactivate");
        await Expect(page.Locator("tbody tr").Last.Locator("form")).ToHaveAttributeAsync("action", $"/products/{inactive.Id}/reactivate");
        await Expect(page.Locator("tbody tr").First.Locator("strong bdi")).ToHaveTextAsync(active.Name);
        await Expect(page.Locator("a[href='/products/create']")).ToBeVisibleAsync();
        await Expect(page.Locator("input[name=includeInactive]")).ToBeCheckedAsync();
        await Expect(page.Locator("nav.pagination a")).ToHaveCountAsync(1);
        await CheckLayout(page, culture, width, "products-clean");
        EventHandler<IDialog> dismiss = async (_, dialog) => await dialog.DismissAsync();
        page.Dialog += dismiss;
        await page.Locator("tbody tr").First.Locator("form button").ClickAsync();
        page.Dialog -= dismiss;
        Assert.Equal(0, mutations);

        await page.GotoAsync("/business-costs?month=2026-10");
        await Expect(page.Locator("#month")).ToHaveValueAsync("2026-10");
        await Expect(page.Locator("[data-add-cost]")).ToHaveAttributeAsync("href", "/business-costs/create?month=2026-10");
        var amounts = CultureInfo.GetCultureInfo(culture);
        await Expect(page.Locator(".cost-total-amount")).ToHaveTextAsync(150m.ToString("N2", amounts) + " IQD");
        await Expect(page.Locator(".cost-recorded-total")).ToContainTextAsync(125m.ToString("N2", amounts));
        await Expect(page.Locator(".cost-recorded-total")).ToContainTextAsync(25m.ToString("N2", amounts));
        await Expect(page.Locator(".cost-inventory-summary")).ToContainTextAsync(900m.ToString("N2", amounts));
        await Expect(page.Locator(".cost-entry")).ToHaveCountAsync(3);
        await Expect(page.Locator(".cost-entry").First.Locator("h3 bdi")).ToHaveTextAsync(rent.Name);
        await Expect(page.Locator(".cost-entry .cost-estimated")).ToHaveCountAsync(1);
        await Expect(page.Locator(".cost-entry").First.Locator($"a[href='/business-costs/{rent.Id}/edit']")).ToBeVisibleAsync();
        await Expect(page.Locator(".cost-entry").First.Locator($"a[href='/business-costs/{rent.Id}/delete']")).ToBeVisibleAsync();
        Assert.Null(await page.Locator("[data-month-review]").GetAttributeAsync("open"));
        var totalsBox = (await page.Locator(".cost-month-summary").BoundingBoxAsync())!;
        var entriesBox = (await page.Locator(".cost-entry-list").BoundingBoxAsync())!;
        var reviewBox = (await page.Locator("[data-month-review]").BoundingBoxAsync())!;
        Assert.True(totalsBox.Y < entriesBox.Y && entriesBox.Y < reviewBox.Y);
        await CheckLayout(page, culture, width, "costs-clean");
        await page.Locator(".cost-entry-details").First.Locator("summary").ClickAsync();
        await Expect(page.Locator(".cost-entry-details").First).ToContainTextAsync(rent.Note!);
        await page.Locator("[data-month-review] > summary").ClickAsync();
        await Expect(page.Locator("[data-cost-review] button")).ToBeVisibleAsync();
        await Expect(page.Locator("[data-cost-review] input[name=version]")).ToHaveValueAsync("7");
        await Expect(page.Locator("[data-cost-review] input[name=month]")).ToHaveValueAsync("2026-10");
        Assert.NotEmpty(await page.Locator("[data-cost-review] input[name=__RequestVerificationToken]").InputValueAsync());
        await page.Locator("[data-add-cost]").ClickAsync();
        await page.WaitForURLAsync("**/business-costs/create?month=2026-10");
        await Expect(page.Locator("#Month")).ToHaveValueAsync("2026-10");
        await page.Locator("#Name").FillAsync("New owner cost");
        await page.Locator("#Amount").FillAsync("50");
        await page.Locator("main a[href='/business-costs?month=2026-10']").ClickAsync();
        Assert.Equal(0, mutations); // Opening/cancelling never submits a mutation.
        await page.Locator("#month").FillAsync("2026-11");
        await page.Locator(".cost-month-toolbar form button").ClickAsync();
        await page.WaitForURLAsync("**/business-costs?month=2026-11");
        await Expect(page.Locator("[data-add-cost]")).ToHaveAttributeAsync("href", "/business-costs/create?month=2026-11");
        await Expect(page.Locator(".cost-entry")).ToHaveCountAsync(0);
        await Expect(page.Locator("[data-cost-review]")).ToHaveCountAsync(0);
        await CheckLayout(page, culture, width, "costs-empty-clean");
        await page.GotoAsync("/business-costs?manager=true");
        await Expect(page.Locator("[data-cost-review]")).ToHaveCountAsync(0);
    }
}
