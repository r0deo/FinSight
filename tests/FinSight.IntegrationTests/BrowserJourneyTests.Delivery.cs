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
    public async Task Delivery_CardsDirtyGuardModalAndSnapshots_WorkForManager(string culture, int width)
    {
        string Money(decimal amount) => amount.ToString("N2", CultureInfo.GetCultureInfo(culture));
        var email = $"delivery-browser-{Guid.NewGuid():N}@example.com";
        var tenant = Guid.NewGuid(); var productId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            db.Businesses.Add(new Business(tenant, "Delivery café", "Duhok", "IQD", email));
            db.Products.Add(new Product(productId, tenant, "Coffee", "Drinks", null, 6000, 3000));
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
        await SignInForJourney(page, email);

        await page.GotoAsync("/simulations/preview");
        await Expect(page.Locator(".decision-card[aria-current]")).ToHaveCountAsync(0);
        await Expect(page.Locator("[data-start-simulation]")).ToBeDisabledAsync();
        await Expect(page.Locator("dialog, [data-decision-results]")).ToHaveCountAsync(0);
        await page.Locator($"input[name=productId][value='{productId}']").CheckAsync();
        await Expect(page.Locator("[data-start-simulation]")).ToBeDisabledAsync();
        await Expect(page.Locator("[data-decision-form], [data-discount-form], [data-delivery-form]")).ToHaveCountAsync(0);
        await page.Locator("input[name=decision][value=pricing]").CheckAsync();
        await Expect(page.Locator("[data-start-simulation]")).ToBeEnabledAsync();
        await page.Locator("[data-start-simulation]").ClickAsync();
        await page.WaitForURLAsync($"**/simulations/preview?productId={productId}");
        await Expect(page.Locator(".decision-product-summary bdi")).ToBeVisibleAsync();
        await Expect(page.Locator(".simulation-destination[aria-current=page]")).ToHaveCountAsync(1);
        await Expect(page.Locator(".decision-selected")).ToHaveCountAsync(0);
        await Expect(page.Locator("dialog, [data-decision-results]")).ToHaveCountAsync(0);
        await Expect(page.Locator(".decision-card")).ToHaveCountAsync(3);
        await Expect(page.Locator(".decision-card[aria-current='page']")).ToHaveCountAsync(1);
        await Expect(page.Locator("[data-decision-form]")).ToHaveCountAsync(1);
        await Expect(page.Locator("[data-delivery-form], [data-discount-form]")).ToHaveCountAsync(0);
        var deliveryCard = page.Locator($".decision-card[href='/simulations/delivery?productId={productId}']");
        await deliveryCard.FocusAsync();
        Assert.NotEqual("none", await deliveryCard.EvaluateAsync<string>("e => getComputedStyle(e).outlineStyle"));
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForURLAsync(url => url.Contains("/simulations/delivery"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(page.Locator(".decision-card[aria-current='page']")).ToHaveAttributeAsync("href", $"/simulations/delivery?productId={productId}");
        await Expect(page.Locator("#UnitsPerOrder")).ToHaveValueAsync("1");
        await Expect(page.Locator("#CommissionPercentage")).ToHaveValueAsync("");
        await Expect(page.Locator("[data-delivery-form]")).ToHaveCountAsync(1);
        await Expect(page.Locator("[data-decision-form], [data-discount-form]")).ToHaveCountAsync(0);
        await page.Locator("#DeliveryUnitPrice").FillAsync("6000");
        await page.Locator("#CommissionPercentage").FillAsync("25");
        await page.Locator("#PackagingPerOrder").FillAsync("500");
        await page.Locator("#OtherChargesPerOrder").FillAsync("0");
        await Layout(page, culture, width, "delivery-inputs");
        // Dirty switch cancellation preserves inputs, acceptance navigates without saving.
        var discountCard = page.Locator($".decision-card[href='/simulations/discount?productId={productId}']");
        var sawPrompt = false;
        EventHandler<IDialog> dismiss = async (_, dialog) => { sawPrompt = true; Assert.False(string.IsNullOrWhiteSpace(dialog.Message)); await dialog.DismissAsync(); };
        page.Dialog += dismiss;
        await discountCard.ClickAsync();
        Assert.True(sawPrompt); await Expect(page.Locator("#DeliveryUnitPrice")).ToHaveValueAsync("6000");
        page.Dialog -= dismiss;
        EventHandler<IDialog> accept = async (_, dialog) => await dialog.AcceptAsync();
        page.Dialog += accept; await discountCard.ClickAsync();
        await page.WaitForURLAsync(url => url.Contains("/simulations/discount"), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        page.Dialog -= accept;
        await page.Locator($".decision-card[href='/simulations/delivery?productId={productId}']").ClickAsync();
        await page.Locator("#DeliveryUnitPrice").FillAsync("6000");
        await page.Locator("#CommissionPercentage").FillAsync("101");
        await page.Locator("#PackagingPerOrder").FillAsync("500");
        await page.Locator("#OtherChargesPerOrder").FillAsync("0");
        await page.Locator("[data-delivery-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("#CommissionPercentage")).ToBeFocusedAsync();
        Assert.False(string.IsNullOrWhiteSpace(await page.Locator("[data-valmsg-for='CommissionPercentage']").InnerTextAsync()));
        await Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await page.Locator("#CommissionPercentage").FillAsync(culture == "en" ? "25" : "٢٥");
        await page.Locator("[data-delivery-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-delivery-contribution]")).ToContainTextAsync(Money(1000));
        await Expect(page.Locator("[data-delivery-scenario]")).ToHaveCountAsync(0); // unknown order counts
        await InspectResultModal(page, "delivery-result", "[data-delivery-form]", "[data-delivery-form] button[type=submit]", culture, width);
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AnyAsync(x => x.BusinessId == tenant));
        await page.Locator("#FixedPeriodCost").FillAsync("20000");
        await Expect(page.Locator("[data-result-open='delivery-result']")).ToBeHiddenAsync();
        var comparison = page.Locator(".delivery-comparison");
        if (!await comparison.EvaluateAsync<bool>("e => e.open")) await comparison.Locator("summary").ClickAsync();
        await page.Locator("#CurrentDirectOrders").FillAsync(culture == "en" ? "100" : "١٠٠");
        await page.Locator("#ExpectedDeliveryOrders").FillAsync("50");
        await page.Locator("#TransferredOrders").FillAsync("10");
        await page.Locator("[data-delivery-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-delivery-scenario]")).ToContainTextAsync(Money(300000));
        await Expect(page.Locator("[data-delivery-difference]")).ToHaveTextAsync(Money(0) + " IQD");
        await Layout(page, culture, width, "delivery-period-result");
        await page.Keyboard.PressAsync("Escape");
        await page.Locator("#TransferredOrders").FillAsync("0");
        await Expect(page.Locator("[data-result-open='delivery-result']")).ToBeHiddenAsync();
        await page.Locator("[data-delivery-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-delivery-scenario]")).ToContainTextAsync(Money(330000));
        await page.Locator("[data-delivery-save] button").ClickAsync();
        await page.WaitForURLAsync(url => Guid.TryParse(new Uri(url).Segments.Last(), out _), new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var saved = await db.PricingSimulations.SingleAsync(x => x.BusinessId == tenant);
            id = saved.Id; Assert.Equal(330000, saved.ProposedContribution);
            var product = await db.Products.SingleAsync(x => x.Id == productId); product.UpdateFinancials(7000, 3500);
            await db.SaveChangesAsync();
        }
        await page.GotoAsync($"/simulations/{id}");
        await Expect(page.Locator("[data-delivery-scenario]")).ToContainTextAsync(Money(330000));
        await page.Locator($"a[href='/simulations/{id}/delivery/edit']").ClickAsync();
        await Expect(page.Locator("dialog[open]")).ToHaveCountAsync(0);
        await Expect(page.Locator("#TransferredOrders")).ToHaveValueAsync("0");
        await page.Locator("#TransferredOrders").FillAsync("50");
        await page.Locator("[data-delivery-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-delivery-scenario]")).ToContainTextAsync(Money(180000));
        await page.Keyboard.PressAsync("Escape");
        await page.GotoAsync($"/simulations/{id}");
        await Expect(page.Locator("[data-delivery-scenario]")).ToContainTextAsync(Money(330000));
        Assert.Empty(errors);
    }
}
