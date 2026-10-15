using FinSight.Domain.Entities;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FinSight.IntegrationTests;

public sealed partial class BrowserJourneyTests
{
    [Theory]
    [InlineData("en", 1440)]
    [InlineData("en", 390)]
    [InlineData("ar-IQ", 1440)]
    [InlineData("ar-IQ", 390)]
    [InlineData("ku-Arab-IQ", 1440)]
    [InlineData("ku-Arab-IQ", 390)]
    public async Task ProductCostHelper_ReviewUseSaveReopenAndCancel_WorkInBrowser(string culture, int width)
    {
        var email = $"cost-browser-{Guid.NewGuid():N}@example.com";
        var tenant = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            db.Businesses.Add(new Business(tenant, "Cost helper café", "Duhok", "IQD", email));
            await db.SaveChangesAsync();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync("Owner")) Assert.True((await roles.CreateAsync(new("Owner"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser(Guid.NewGuid(), tenant, email) { EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, "Password1!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Owner")).Succeeded);
        }
        var origin = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { BaseURL = origin, IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = width, Height = 900 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await page.GotoAsync("/account/login");
        await page.Locator("#site-language").SelectOptionAsync(culture);
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", culture);
        await page.Locator("#Email").FillAsync(email); await page.Locator("#Password").FillAsync("Password1!");
        await page.Locator("main form button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/account/login"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(page.Locator("form[action='/account/logout']")).ToBeVisibleAsync();
        await page.GotoAsync("/products/create");
        await page.Locator("#Name").FillAsync("Rice meal"); await page.Locator("#Category").FillAsync("Meals");
        await page.Locator("#SellingPrice").FillAsync("5000"); await page.Locator("#UnitCost").FillAsync("4");
        await page.Locator("[data-cost-helper] summary").ClickAsync();
        await page.Locator("[name='CostHelper.Ingredients[0].Name']").FillAsync("Rice");
        await page.Locator("[name='CostHelper.Ingredients[0].PackQuantity']").FillAsync("1");
        await page.Locator("[name='CostHelper.Ingredients[0].PackUnit']").SelectOptionAsync("Kilogram");
        await page.Locator("[name='CostHelper.Ingredients[0].PackPrice']").FillAsync("12000");
        await page.Locator("[name='CostHelper.Ingredients[0].UsedQuantity']").FillAsync(culture == "en" ? "1000" : "١٠٠٠");
        await page.Locator("[name='CostHelper.Ingredients[0].UsedUnit']").SelectOptionAsync("Gram");
        await page.Locator("[name='CostHelper.UsableOutput']").FillAsync("10");
        await page.Locator("[name='CostHelper.PackagingPerUnit']").FillAsync(culture == "en" ? "200" : "٢٠٠");
        await page.Locator("[name='CostHelper.OtherVariableCostPerUnit']").FillAsync("0");
        await page.Locator("button[value=calculate]").ClickAsync();
        await Expect(page.Locator("[data-product-cost-result]")).ToBeVisibleAsync();
        await Layout(page, culture, width, "product-cost-review");
        await page.Locator("button[value=use]").ClickAsync();
        await Expect(page.Locator("#UnitCost")).ToHaveValueAsync("1400");
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Products.AnyAsync(x => x.BusinessId == tenant));
        await page.Locator("[data-product-save]").ClickAsync();
        await page.WaitForURLAsync("**/products");
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Products.SingleAsync(x => x.BusinessId == tenant);
            id = saved.Id; Assert.Equal(1400m, saved.UnitCost); Assert.NotNull(saved.CostBreakdown);
        }
        await page.GotoAsync($"/products/{id}/edit");
        await page.Locator("[data-cost-helper] summary").ClickAsync();
        await Expect(page.Locator("[name='CostHelper.Ingredients[0].Name']")).ToHaveValueAsync("Rice");
        await page.Locator("[name='CostHelper.Ingredients[0].PackPrice']").FillAsync("18000");
        await Expect(page.Locator("[data-product-cost-result]")).ToBeHiddenAsync();
        await page.Locator("button[value=calculate]").ClickAsync();
        await Expect(page.Locator("[data-product-cost-result]")).ToBeVisibleAsync();
        await page.Locator("button[value=use]").ClickAsync();
        await Expect(page.Locator("#UnitCost")).ToHaveValueAsync("2000");
        await page.Locator("[data-cost-helper] summary").ClickAsync();
        await page.Locator("button[value=cancel]").ClickAsync();
        await Expect(page.Locator("#UnitCost")).ToHaveValueAsync(1400.00m.ToString(System.Globalization.CultureInfo.GetCultureInfo(culture)));
        await page.Locator("[data-product-save]").ClickAsync();
        await page.WaitForURLAsync("**/products?includeInactive=True");
        using (var scope = factory.Services.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().Products.SingleAsync(x => x.Id == id);
            Assert.Equal(1400m, saved.UnitCost);
            Assert.Equal(12000m, saved.CostBreakdown!.Inputs.Ingredients[0].PackPrice);
        }
        Assert.Empty(errors);
    }
}
