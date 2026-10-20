using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.IntegrationTests;

public partial class WebsiteTests
{
    private async Task SeedLinkedCosts(Guid tenant)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
        foreach (var (category, amount) in new[] { (BusinessCostCategory.Rent,1000000m),
            (BusinessCostCategory.Labor,1500000m), (BusinessCostCategory.Energy,500000m),
            (BusinessCostCategory.Inventory,9000000m) })
            db.BusinessCosts.Add(new(Guid.NewGuid(), tenant, category.ToString(), category, amount,
                new(2026,10,1), CostCertainty.Actual, UnitCostInclusion.No, null));
        await db.SaveChangesAsync();
    }
    private static void EnableCostLink(Dictionary<string,string> fields)
    {
        fields["LinkCosts"]="true"; fields["CostMonth"]="2026-10"; fields["AllocateCosts"]="true";
        fields["AllocationPercentage"]="10"; fields["ConfirmCostBasis"]="true";
    }

    [Theory]
    [InlineData("en")] [InlineData("ar-IQ")] [InlineData("ku-Arab-IQ")]
    public async Task OptionalCosts_PricingReferenceSnapshotStalenessAndTenantSafety(string culture)
    {
        string Money(decimal amount) => amount.ToString("N2",CultureInfo.GetCultureInfo(culture));
        using var owner=Browser(); await Login(owner, await RegisterAndConfirm(owner));
        var name="Linked-"+Guid.NewGuid().ToString("N");
        var productFields=ProductFields(name,"10000"); productFields["UnitCost"]="5000";
        await Submit(owner,"/products/create",productFields);
        var product=await FindProduct(name); await SeedLinkedCosts(product.BusinessId);
        var url=$"/simulations/preview?productId={product.Id}";
        await Submit(owner,"/culture",new(){["culture"]=culture,["returnUrl"]=url},url);
        var initial=await owner.GetStringAsync(url);
        Assert.DoesNotContain("checked",Regex.Match(initial,"<input[^>]*id=\"LinkCosts\"[^>]*>").Value);
        var fields=new Dictionary<string,string> {
            ["ProductId"]=product.Id.ToString(), ["ProposedPrice"]="11000", ["CurrentSalesQuantity"]="100",
            ["ExpectedSalesQuantity"]="100",["ComparisonPeriod"]="Month",["DeclinePercentage"]="10",
            ["PreviewCurrentPrice"]="10000",["PreviewUnitCost"]="5000" };
        async Task<string> Preview()
        {
            var response=await Submit(owner,"/simulations/preview",fields,url);
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        }
        var a=await Preview(); Assert.Contains("data-costs-excluded",a); Assert.DoesNotContain("data-overhead-total",a);
        Assert.Contains(Money(600000),a);
        EnableCostLink(fields); fields["AllocateCosts"]="false";
        var b=await Preview(); Assert.Contains(Money(3000000),b); Assert.DoesNotContain("data-allocation-amount",b);
        fields["AllocateCosts"]="true"; fields["AllocationPercentage"]="101";
        var invalid=await Submit(owner,"/simulations/preview",fields,url);
        Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        Assert.DoesNotContain("id=\"pricing-result\"",await invalid.Content.ReadAsStringAsync());
        fields["AllocationPercentage"]=culture=="en" ? "10" : "١٠";
        var c=await Preview(); Assert.Contains("data-allocation-amount",c); Assert.Contains(Money(200000),c);
        Assert.Contains(Money(300000),c);
        fields["AdditionalDecisionCost"]="50000";
        var d=await Preview(); Assert.Contains(Money(550000),d); Assert.Contains(Money(250000),d);
        fields["CostBasis"]=CostInput(d,"CostBasis"); Assert.NotEmpty(fields["CostBasis"]);
        // Changing settings under the old preview cannot silently save a different basis.
        fields["LinkCosts"]="false";
        Assert.Equal(HttpStatusCode.Conflict,(await Submit(owner,"/simulations/preview/save",fields,url)).StatusCode);
        fields["AllocationPercentage"]="stale-disabled";
        var e=await Preview(); Assert.Contains(Money(550000),e); Assert.Contains("data-costs-excluded",e);
        Assert.DoesNotContain("data-allocation-amount",e);
        EnableCostLink(fields); d=await Preview(); fields["CostBasis"]=CostInput(d,"CostBasis");
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            Assert.False(await db.PricingSimulations.AnyAsync(x=>x.ProductId==product.Id));
            var rent=await db.BusinessCosts.SingleAsync(x=>x.BusinessId==product.BusinessId && x.Category==BusinessCostCategory.Rent);
            // Change the expense version without changing its amount: still requires recalculation.
            rent.Update("Updated rent",rent.Category,rent.Amount,rent.Month,rent.Certainty,rent.Inclusion,null);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict,(await Submit(owner,"/simulations/preview/save",fields,url)).StatusCode);
        d=await Preview(); fields["CostBasis"]=CostInput(d,"CostBasis");
        var saved=await Submit(owner,"/simulations/preview/save",fields,url);
        Assert.Equal(HttpStatusCode.Redirect,saved.StatusCode); var location=saved.Headers.Location!.OriginalString;
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var simulation=await db.PricingSimulations.SingleAsync(x=>x.ProductId==product.Id);
            Assert.Equal(550000,simulation.ProposedContribution); Assert.Equal(50000,simulation.Difference);
            Assert.Equal(50000,simulation.AdditionalDecisionCost); Assert.Equal(3000000,simulation.CostContext!.RecordedBasis);
            Assert.Equal(300000,simulation.CostContext.AllocationAmount);
            var rent=await db.BusinessCosts.SingleAsync(x=>x.BusinessId==product.BusinessId && x.Category==BusinessCostCategory.Rent);
            rent.Update("Changed later",rent.Category,2,rent.Month,rent.Certainty,rent.Inclusion,null);
            await db.SaveChangesAsync();
        }
        var detail=WebUtility.HtmlDecode(await owner.GetStringAsync(location));
        Assert.Contains(Money(3000000),detail); Assert.Contains(Money(250000),detail);
        using var other=Browser(); await Login(other,await RegisterAndConfirm(other));
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Submit(other,"/simulations/preview",fields,"/products")).StatusCode);
    }

    [Fact]
    public async Task LinkedWhatIf_PersistsValidatedSnapshot_AndRetryCannotChangeAllocation()
    {
        using var owner=Browser(); await Login(owner,await RegisterAndConfirm(owner));
        var name="LinkedWhatIf-"+Guid.NewGuid().ToString("N");
        var p=ProductFields(name,"10000"); p["UnitCost"]="5000"; await Submit(owner,"/products/create",p);
        var product=await FindProduct(name); await SeedLinkedCosts(product.BusinessId);
        var url=$"/what-if?productId={product.Id}";
        var fields=new Dictionary<string,string> {
            ["ProductId"]=product.Id.ToString(),["CurrentQuantity"]="100",["Period"]="Month",
            ["Concern"]="Supplier",["CostInputMode"]="Percentage",["CostIncreasePercent"]="20",
            ["PreviewPrice"]="10000",["PreviewCost"]="5000" };
        EnableCostLink(fields);
        var preview=await Submit(owner,"/what-if/preview",fields,url);
        Assert.Equal(HttpStatusCode.OK,preview.StatusCode);
        var html=await preview.Content.ReadAsStringAsync();
        fields["CostBasis"]=CostInput(html,"CostBasis"); fields["PreviewProof"]=CostInput(html,"PreviewProof");
        Assert.NotEmpty(fields["CostBasis"]); Assert.NotEmpty(fields["PreviewProof"]);
        var saved=await Submit(owner,"/what-if/save",fields,url);
        Assert.Equal(HttpStatusCode.Redirect,saved.StatusCode);
        Assert.Equal(saved.Headers.Location,(await Submit(owner,"/what-if/save",fields,url)).Headers.Location);
        fields["AllocateCosts"]="false";
        Assert.Equal(HttpStatusCode.Conflict,(await Submit(owner,"/what-if/save",fields,url)).StatusCode);
        using(var scope=factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<FinSightDbContext>();
            var simulation=await db.WhatIfSimulations.SingleAsync(x=>x.ProductId==product.Id);
            Assert.Equal(300000,simulation.CostContext!.AllocationAmount);
            Assert.Equal(200000,simulation.CostContext.CurrentRemainder);
            Assert.Equal(100000,simulation.CostContext.Options.Single().Remainder);
            var rent=await db.BusinessCosts.SingleAsync(x=>x.BusinessId==product.BusinessId && x.Category==BusinessCostCategory.Rent);
            rent.Update("Later",rent.Category,1,rent.Month,rent.Certainty,rent.Inclusion,null); await db.SaveChangesAsync();
        }
        Assert.Contains("3,000,000.00",WebUtility.HtmlDecode(await owner.GetStringAsync(saved.Headers.Location)));
        using var other=Browser(); await Login(other,await RegisterAndConfirm(other));
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync(saved.Headers.Location)).StatusCode);
    }

    [Theory]
    [InlineData("discount")] [InlineData("delivery")]
    public async Task LinkedDecisions_SaveAndEditRestoreSettings_AndKeepOriginalProductSnapshot(string kind)
    {
        using var owner=Browser(); await Login(owner,await RegisterAndConfirm(owner));
        var name="LinkedEdit-"+Guid.NewGuid().ToString("N");
        var p=ProductFields(name,"6000"); p["UnitCost"]="3000"; await Submit(owner,"/products/create",p);
        var product=await FindProduct(name); await SeedLinkedCosts(product.BusinessId);
        var url=$"/simulations/{kind}?productId={product.Id}";
        var fields=kind=="delivery" ? DeliveryFields(product.Id) : new Dictionary<string,string>{
            ["ProductId"]=product.Id.ToString(),["DiscountPercentage"]="10",["CurrentSalesQuantity"]="100",
            ["ExpectedSalesQuantity"]="140",["CampaignCost"]="0",["ComparisonPeriod"]="Month",
            ["PreviewCurrentPrice"]="6000",["PreviewUnitCost"]="3000" };
        EnableCostLink(fields);
        var preview=await Submit(owner,$"/simulations/{kind}",fields,url);
        Assert.Equal(HttpStatusCode.OK,preview.StatusCode);
        fields["CostBasis"]=CostInput(await preview.Content.ReadAsStringAsync(),"CostBasis");
        var save=await Submit(owner,$"/simulations/{kind}/save",fields,url);
        Assert.Equal(HttpStatusCode.Redirect,save.StatusCode);
        var location=save.Headers.Location!.OriginalString;
        var edit=location+$"/{kind}/edit";
        var html=await owner.GetStringAsync(edit);
        Assert.Contains("checked",Regex.Match(html,"<input[^>]*id=\"LinkCosts\"[^>]*>").Value);
        Assert.Contains("checked",Regex.Match(html,"<input[^>]*id=\"AllocateCosts\"[^>]*>").Value);
        Assert.Equal("10",CostInput(html,"AllocationPercentage"));
        p["UnitCost"]="4000"; await Submit(owner,$"/products/{product.Id}/edit",p);
        fields["Version"]="1";
        var updated=await Submit(owner,edit,fields);
        Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        fields["CostBasis"]=CostInput(await updated.Content.ReadAsStringAsync(),"CostBasis");
        Assert.Equal(HttpStatusCode.Redirect,(await Submit(owner,location+$"/{kind}/save",fields,edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Submit(owner,location+$"/{kind}/save",fields,edit)).StatusCode);
        using var scope=factory.Services.CreateScope();
        var saved=await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations
            .SingleAsync(x=>x.ProductId==product.Id);
        Assert.Equal(3000,saved.UnitCost); Assert.True(saved.CostContext!.Settings.Enabled); Assert.Equal(2,saved.Version);
    }
}
