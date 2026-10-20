using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
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
    [InlineData("en",1440)] [InlineData("en",390)]
    [InlineData("ar-IQ",1440)] [InlineData("ar-IQ",390)]
    [InlineData("ku-Arab-IQ",1440)] [InlineData("ku-Arab-IQ",390)]
    public async Task OptionalCosts_NavigationAllocationModalAndExplicitSave(string culture,int width)
    {
        string Money(decimal amount)=>amount.ToString("N2",CultureInfo.GetCultureInfo(culture));
        var email=$"cost-link-{Guid.NewGuid():N}@example.com";
        var tenant=Guid.NewGuid(); var productId=Guid.NewGuid();
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            db.Businesses.Add(new Business(tenant,"Cost link café","Duhok","IQD",email));
            db.Products.Add(new Product(productId,tenant,"Coffee","Drinks",null,10000,5000));
            foreach(var (category,amount) in new[]{(BusinessCostCategory.Rent,1000000m),
                (BusinessCostCategory.Labor,1500000m),(BusinessCostCategory.Energy,500000m)})
                db.BusinessCosts.Add(new(Guid.NewGuid(),tenant,category.ToString(),category,amount,new(2026,10,1),CostCertainty.Actual,UnitCostInclusion.No,null));
            await db.SaveChangesAsync();
            var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            if(!await roles.RoleExistsAsync("Owner")) Assert.True((await roles.CreateAsync(new("Owner"))).Succeeded);
            var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user=new ApplicationUser(Guid.NewGuid(),tenant,email){EmailConfirmed=true};
            Assert.True((await users.CreateAsync(user,"Password1!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user,"Owner")).Succeeded);
        }
        var origin=factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var playwright=await Playwright.CreateAsync();
        await using var browser=await playwright.Chromium.LaunchAsync(new(){Headless=true});
        await using var context=await browser.NewContextAsync(new(){BaseURL=origin,IgnoreHTTPSErrors=true,ViewportSize=new(){Width=width,Height=900}});
        var page=await context.NewPageAsync(); var errors=new List<string>();
        page.PageError+=(_,error)=>errors.Add(error);
        await page.GotoAsync("/account/login"); await page.Locator("#site-language").SelectOptionAsync(culture);
        await Expect(page.Locator("html")).ToHaveAttributeAsync("lang",culture);
        await page.Locator("#Email").FillAsync(email); await page.Locator("#Password").FillAsync("Password1!");
        await page.Locator("main form button[type=submit]").ClickAsync();
        await page.WaitForURLAsync(url=>!url.Contains("/account/login"));
        await page.GotoAsync($"/simulations/preview?productId={productId}");
        await Expect(page.Locator(".simulation-destination")).ToHaveCountAsync(3);
        await Expect(page.Locator(".simulation-destination[aria-current='page']")).ToHaveCountAsync(1);
        await Expect(page.Locator(".decision-card")).ToHaveCountAsync(3);
        await Expect(page.Locator("#LinkCosts")).Not.ToBeCheckedAsync();
        await Expect(page.Locator("#CostMonth")).ToBeHiddenAsync();
        await page.Locator("#ProposedPrice").FillAsync("11000");
        await page.Locator("#CurrentSalesQuantity").FillAsync("100");
        await page.Locator("#ComparisonPeriod").SelectOptionAsync("Month");
        var sawPrompt=false;
        EventHandler<IDialog> dismiss=async(_,dialog)=>{sawPrompt=true; await dialog.DismissAsync();};
        page.Dialog+=dismiss;
        await page.Locator($".simulation-destination[href='/what-if?productId={productId}']").ClickAsync();
        Assert.True(sawPrompt); await Expect(page.Locator("#ProposedPrice")).ToHaveValueAsync("11000");
        page.Dialog-=dismiss;
        await page.Locator("#LinkCosts").CheckAsync();
        await Expect(page.Locator("#AllocateCosts")).Not.ToBeCheckedAsync();
        await page.Locator("#CostMonth").FillAsync("2026-10");
        await page.Locator("#AllocateCosts").CheckAsync();
        await Expect(page.Locator("#AllocationPercentage")).ToHaveValueAsync("");
        await page.Locator("#AllocationPercentage").FillAsync(culture=="en"?"10":"١٠");
        await page.Locator("#ConfirmCostBasis").CheckAsync();
        await page.Locator("#AdditionalDecisionCost").FillAsync("50000");
        await Layout(page,culture,width,"optional-costs-inputs");
        await page.Locator("[data-decision-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-overhead-total]")).ToContainTextAsync(Money(3000000));
        await Expect(page.Locator("[data-allocation-amount]")).ToContainTextAsync(Money(300000));
        await Expect(page.Locator("[data-current-remainder]")).ToContainTextAsync(Money(200000));
        await Expect(page.Locator("[data-proposed-remainder]").First).ToContainTextAsync(Money(250000));
        await InspectResultModal(page,"pricing-result","[data-decision-form]","[data-decision-form] button[type=submit]",culture,width);
        using(var scope=factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AnyAsync(x=>x.BusinessId==tenant));
        await page.Locator("#LinkCosts").UncheckAsync();
        await Expect(page.Locator("[data-result-open='pricing-result']")).ToBeHiddenAsync();
        await Expect(page.Locator("#AdditionalDecisionCost")).ToHaveValueAsync("50000");
        await page.Locator("[data-decision-form] button[type=submit]").ClickAsync();
        await Expect(page.Locator("[data-costs-excluded]")).ToBeVisibleAsync();
        await Expect(page.Locator("[data-allocation-amount]")).ToHaveCountAsync(0);
        await Expect(page.Locator("[data-decision-results]")).ToContainTextAsync(Money(550000));
        await page.Keyboard.PressAsync("Escape");
        await page.Locator("#LinkCosts").CheckAsync(); await page.Locator("#CostMonth").FillAsync("2026-10");
        await page.Locator("#AllocateCosts").CheckAsync(); await page.Locator("#AllocationPercentage").FillAsync("10");
        await page.Locator("#ConfirmCostBasis").CheckAsync();
        await page.Locator("[data-decision-form] button[type=submit]").ClickAsync();
        await page.Locator("[data-decision-save] button").ClickAsync();
        await page.WaitForURLAsync(url=>!url.Contains("preview"));
        await Expect(page.Locator("[data-allocation-amount]")).ToContainTextAsync(Money(300000));
        await Layout(page,culture,width,"optional-costs-saved");
        await page.Locator("[data-linked-cost-result]").ScreenshotAsync(new() {
            Path=Path.Combine(AppContext.BaseDirectory,"TestResults","browser",$"{culture}-{width}-allocated-context.png") });
        await page.Locator(".simulation-destination[href='/simulations']").ClickAsync();
        await Expect(page.Locator(".decision-card")).ToHaveCountAsync(0);
        await Expect(page.Locator(".simulation-destination[aria-current='page']")).ToHaveAttributeAsync("href","/simulations");
        await page.Locator(".simulation-destination[href='/what-if']").ClickAsync();
        await page.Locator($"a[href='/what-if?productId={productId}']").ClickAsync();
        await Expect(page.Locator("#LinkCosts")).Not.ToBeCheckedAsync();
        await page.Locator("#CurrentQuantity").FillAsync("100");
        page.Dialog+=dismiss; sawPrompt=false;
        await page.Locator($".simulation-destination[href='/simulations/preview?productId={productId}']").ClickAsync();
        Assert.True(sawPrompt); await Expect(page.Locator("#CurrentQuantity")).ToHaveValueAsync("100");
        page.Dialog-=dismiss;
        EventHandler<IDialog> accept=async(_,dialog)=>await dialog.AcceptAsync();
        page.Dialog+=accept;
        await page.Locator($".simulation-destination[href='/simulations/preview?productId={productId}']").ClickAsync();
        await page.WaitForURLAsync(url=>url.Contains("simulations/preview"));
        page.Dialog-=accept;
        Assert.Empty(errors);
    }
}
