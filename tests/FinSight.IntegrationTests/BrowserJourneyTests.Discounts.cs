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
using System.Globalization;

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
    public async Task Discount_ManagerCanCalculateCloseRecalculateSaveAndEdit(string culture, int width)
    {
        string Money(decimal amount) => amount.ToString("N2", CultureInfo.GetCultureInfo(culture));
        var email = $"discount-browser-{Guid.NewGuid():N}@example.com";
        var tenant = Guid.NewGuid(); var productId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            db.Businesses.Add(new Business(tenant, "Discount café", "Duhok", "IQD", email));
            db.Products.Add(new Product(productId, tenant, "Coffee", "Drinks", null, 5000, 3000));
            await db.SaveChangesAsync();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if (!await roles.RoleExistsAsync("Manager")) Assert.True((await roles.CreateAsync(new("Manager"))).Succeeded);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser(Guid.NewGuid(), tenant, email) { EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, "Password1!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Manager")).Succeeded);
        }
        var origin = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { BaseURL = origin, IgnoreHTTPSErrors = true,
            ViewportSize = new() { Width = width, Height = 900 } });
        var page = await context.NewPageAsync(); var errors = new List<string>();
        page.PageError += (_, error) => errors.Add(error);
        await page.GotoAsync("/account/login");
        await page.Locator("#site-language").SelectOptionAsync(culture);
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang", culture);
        await page.Locator("#Email").FillAsync(email); await page.Locator("#Password").FillAsync("Password1!");
        await page.Locator("main form button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/account/login"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.GotoAsync("/simulations");
        await Expect(page.Locator("header a[href='/simulations/discount']")).ToHaveCountAsync(0);
        await page.Locator("main a[href='/simulations/discount']").ClickAsync();
        await page.Locator($"a[href='/simulations/discount?productId={productId}']").ClickAsync();
        await Layout(page, culture, width, "discount-inputs");
        await page.Locator("#DiscountPercentage").FillAsync("0");
        await page.Locator("#CurrentSalesQuantity").FillAsync(culture == "en" ? "100" : "١٠٠");
        await page.Locator("#ExpectedSalesQuantity").FillAsync("140");
        await page.Locator("#CampaignCost").FillAsync("0");
        await page.Locator("[data-discount-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("#DiscountPercentage")).ToBeFocusedAsync();
        Assert.False(string.IsNullOrWhiteSpace(await page.Locator("[data-valmsg-for='DiscountPercentage']").InnerTextAsync()));
        await Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await page.Locator("#DiscountPercentage").FillAsync(culture == "en" ? "10" : "١٠");
        await page.Locator("[data-discount-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-discount-price]")).ToContainTextAsync(Money(4500));
        await Expect(page.Locator("[data-discount-threshold]")).ToContainTextAsync("134");
        await Expect(page.Locator("[data-discount-difference]")).ToContainTextAsync(Money(10000));
        await InspectResultModal(page, "discount-result", "[data-discount-form]", "[data-discount-form] button[type=submit]", culture, width);
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AnyAsync(x => x.BusinessId == tenant));
        await page.Locator("#CampaignCost").FillAsync("30000");
        await Expect(page.Locator("[data-result-open='discount-result']")).ToBeHiddenAsync();
        await page.Locator("[data-discount-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-discount-threshold]")).ToContainTextAsync("154");
        await Expect(page.Locator("[data-discount-difference]")).ToContainTextAsync(Money(-20000));
        await page.Locator("[data-discount-save] button").ClickAsync();
        await page.WaitForURLAsync(url => Guid.TryParse(new Uri(url).Segments.Last(), out _), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var saved = await db.PricingSimulations.SingleAsync(x => x.BusinessId == tenant);
            id = saved.Id; Assert.Equal(180000, saved.ProposedContribution);
            var product = await db.Products.SingleAsync(x => x.Id == productId); product.UpdateFinancials(6000, 3500);
            await db.SaveChangesAsync();
        }
        await page.GotoAsync($"/simulations/{id}");
        await Expect(page.Locator("[data-discount-price]")).ToContainTextAsync(Money(4500));
        await page.Locator($"a[href='/simulations/{id}/discount/edit']").ClickAsync();
        await Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await Expect(page.Locator("#CampaignCost")).ToHaveValueAsync("30000");
        await page.Locator("#ExpectedSalesQuantity").FillAsync("120");
        await page.Locator("[data-discount-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-discount-results]")).ToContainTextAsync(Money(150000));
        await page.Keyboard.PressAsync("Escape");
        await page.GotoAsync($"/simulations/{id}");
        await Expect(page.Locator("[data-discount-results]")).ToContainTextAsync(Money(180000));
        Assert.Empty(errors);
    }
}
