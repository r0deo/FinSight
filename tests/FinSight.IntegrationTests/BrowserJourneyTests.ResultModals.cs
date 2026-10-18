using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FinSight.IntegrationTests;

public sealed partial class BrowserJourneyTests
{
    private static async Task InspectResultModal(IPage page, string id, string source, string trigger, string culture, int width)
    {
        var dialog = page.Locator("dialog#" + id);
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(page.Locator("dialog[open]")).ToHaveCountAsync(1);
        await Expect(dialog).ToHaveAttributeAsync("aria-labelledby", id + "-title");
        Assert.False(string.IsNullOrWhiteSpace(await page.Locator("#" + id + "-title").InnerTextAsync()));
        await Expect(dialog.Locator("[data-result-close]")).ToBeFocusedAsync();
        var values = await page.Locator(source).EvaluateAsync<string>("form => JSON.stringify([...new FormData(form)])");
        // Native focus containment: tabbing through long results cannot reach the page behind.
        for (var i = 0; i < 8; i++)
        {
            await page.Keyboard.PressAsync(i == 0 ? "Shift+Tab" : "Tab");
            Assert.True(await dialog.EvaluateAsync<bool>("dialog => dialog.contains(document.activeElement)"));
        }
        var box = (await dialog.BoundingBoxAsync())!;
        Assert.True(box.X >= 0 && box.Y >= 0 && box.Width <= width && box.Height <= 900);
        Assert.True(await dialog.EvaluateAsync<bool>("dialog => dialog.scrollWidth <= dialog.clientWidth + 1"));
        var body = dialog.Locator(".result-modal-body");
        if (await body.EvaluateAsync<bool>("body => body.scrollHeight > body.clientHeight + 1"))
        {
            await body.EvaluateAsync("body => body.scrollTop = body.scrollHeight");
            Assert.True(await body.EvaluateAsync<bool>("body => body.scrollTop > 0"));
            await Expect(dialog.Locator("[data-result-close]")).ToBeInViewportAsync();
        }
        await body.EvaluateAsync("body => body.scrollTop = 0");
        await Layout(page, culture, width, id + "-modal");
        await body.EvaluateAsync("body => body.scrollTop = 0");
        await page.ScreenshotAsync(new() { Path = Path.Combine(AppContext.BaseDirectory, "TestResults", "browser", $"{culture}-{width}-{id}-viewport.png") });
        await page.Keyboard.PressAsync("Escape");
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(page.Locator(trigger)).ToBeFocusedAsync();
        Assert.Equal(values, await page.Locator(source).EvaluateAsync<string>("form => JSON.stringify([...new FormData(form)])"));
        var reopen = page.Locator("[data-result-open='" + id + "']");
        await reopen.ClickAsync();
        await Expect(dialog).ToBeVisibleAsync();
        await dialog.Locator("[data-result-close]").ClickAsync();
        await Expect(dialog).ToBeHiddenAsync();
        await Expect(reopen).ToBeFocusedAsync();
        Assert.Equal(values, await page.Locator(source).EvaluateAsync<string>("form => JSON.stringify([...new FormData(form)])"));
    }
}
