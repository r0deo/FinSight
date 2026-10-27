using FinSight.Application.Features.Products;
using FinSight.Application.Features.Products.Queries.GetProducts;
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
    public async Task Home_FirstStepsActionsPathAndReducedMotion(string culture, int width)
    {
        var empty = await Render("Home/Welcome", "Home", "/home", new GetProductsResponse([], 1, 1, 0, 0), culture, owner: true);
        var product = new ProductResponse(Guid.NewGuid(), "Owner's product", "Coffee", null, 4000, 1000, true);
        var existing = await Render("Home/Welcome", "Home", "/home", new GetProductsResponse([product], 1, 1, 1, 1), culture, owner: true);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new()
        { BaseURL = factory.Origin, IgnoreHTTPSErrors = true, ViewportSize = new() { Width = width, Height = 900 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.RouteAsync("**/home*", async route =>
        {
            // Do not intercept the real home-path static assets.
            if (new Uri(route.Request.Url).AbsolutePath != "/home") { await route.ContinueAsync(); return; }
            await route.FulfillAsync(new() { ContentType = "text/html; charset=utf-8", Body = route.Request.Url.Contains("existing") ? existing : empty });
        });
        foreach (var state in new[] { "empty", "existing" })
        {
            await page.GotoAsync($"/home?state={state}");
            await Expect(page.Locator(".home-step")).ToHaveCountAsync(2);
            await Expect(page.Locator(".home-step-active")).ToHaveCountAsync(1);
            var primary = page.Locator("[data-path-action]");
            await Expect(primary).ToHaveAttributeAsync("href", state == "empty" ? "/products/create" : "/simulations/preview");
            await Expect(page.Locator(".home-next .button-secondary")).ToHaveCountAsync(state == "empty" ? 0 : 1);
            await Expect(page.Locator(".home-step h2 a").First).ToHaveAttributeAsync("href", "/products/create");
            await Expect(page.Locator(".home-step h2 a").Last).ToHaveAttributeAsync("href", "/simulations/preview");
            if (culture != "en") Assert.DoesNotContain("Enter its selling price", await page.Locator(".home-path").InnerTextAsync());
            await page.WaitForFunctionAsync("document.querySelector('.home-path-art.is-ready') && parseFloat(document.querySelector('.home-path-line').style.strokeDashoffset) > 0");
            await Expect(primary).ToBeVisibleAsync();
            await page.WaitForFunctionAsync("document.querySelector('.home-path-line').style.strokeDashoffset === '0'");
            // The point settles at the primary button, not elsewhere after an RTL/mobile reflow.
            var dot = (await page.Locator(".home-path-point").BoundingBoxAsync())!;
            var button = (await primary.BoundingBoxAsync())!;
            Assert.InRange(Math.Abs(dot.X + dot.Width / 2 - button.X - button.Width / 2), 0, 2);
            Assert.InRange(button.Y - dot.Y - dot.Height / 2, 4, 6);
            await primary.FocusAsync();
            await Expect(primary).ToBeFocusedAsync();
            await Expect(primary).ToHaveCSSAsync("outline-style", "solid");
            await page.Locator(".home-step-active a").HoverAsync();
            Assert.Equal("0", await page.Locator(".home-path-line").EvaluateAsync<string>("e => e.style.strokeDashoffset"));
            await CheckLayout(page, culture, width, $"home-{state}");
            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
            await page.ReloadAsync();
            await Expect(page.Locator(".home-path-art")).ToHaveClassAsync("home-path-art is-ready");
            Assert.Equal("0", await page.Locator(".home-path-line").EvaluateAsync<string>("e => e.style.strokeDashoffset"));
            await page.SetViewportSizeAsync(width == 390 ? 1440 : 390, 900);
            await CheckLayout(page, culture, width, $"home-{state}-reduced-reflow");
            Assert.Equal("0", await page.Locator(".home-path-line").EvaluateAsync<string>("e => e.style.strokeDashoffset"));
            await page.SetViewportSizeAsync(width, 900);
            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.NoPreference });
        }
        Assert.Empty(errors);
    }
}
