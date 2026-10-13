using System.Collections;
using System.Globalization;
using System.Resources;
using FinSight.Api.Website.Controllers;
using FinSight.Api.Website.Localization;
using FinSight.Domain.Services;
using FinSight.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FinSight.UnitTests.Website;

public class LocalizationTests
{
    [Theory]
    [InlineData("en", "ltr")]
    [InlineData("ar-IQ", "rtl")]
    [InlineData("ku-Arab-IQ", "rtl")]
    public void SupportedCultures_HaveExpectedDirection(string tag, string direction)
    {
        var culture = CultureInfo.GetCultureInfo(tag);
        Assert.Equal(tag, culture.Name);
        Assert.Equal(direction == "rtl", WebsiteCultures.IsRightToLeft(culture));
    }

    [Fact]
    public void ResourceSets_AreCompleteForAllSupportedCultures()
    {
        AssertComplete(typeof(RebazText), "FinSight.Api.Resources.Website.Localization.RebazText");
        AssertComplete(typeof(IdentityEmailText), "FinSight.Infrastructure.Resources.Identity.IdentityEmailText");
        var manager = new ResourceManager("FinSight.Api.Resources.Website.Localization.RebazText", typeof(RebazText).Assembly);
        Assert.Equal("بەرهەم", manager.GetString("Products", CultureInfo.GetCultureInfo(WebsiteCultures.Badini)));
        Assert.Equal("Products", manager.GetString("Products", CultureInfo.GetCultureInfo("fr-FR")));
    }

    [Fact]
    public void Localizer_UsesBadiniResourceAndFallsBackToEnglish()
    {
        var services = new ServiceCollection().AddLogging()
            .AddLocalization(options => options.ResourcesPath = "Resources")
            .BuildServiceProvider();
        var text = services.GetRequiredService<IStringLocalizer<RebazText>>();
        var emailText = services.GetRequiredService<IStringLocalizer<IdentityEmailText>>();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(WebsiteCultures.Badini);
            Assert.Equal("بەرهەم", text["Products"].Value);
            Assert.Contains("پشتڕاستکرنا", emailText["ConfirmationSubject"].Value);
            Assert.Equal("Only English fallback", text["Only English fallback"].Value);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(WebsiteCultures.Arabic);
            Assert.Equal("المنتجات", text["Products"].Value);
            Assert.Contains("تأكيد", emailText["ConfirmationSubject"].Value);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Theory]
    [InlineData("/products?page=2", "/products?page=2")]
    [InlineData("https://attacker.example", "/")]
    [InlineData("//attacker.example", "/")]
    [InlineData("/api/products", "/")]
    [InlineData("/%2F%2Fattacker.example", "/")]
    [InlineData("/account/google/callback", "/")]
    public void LanguageReturnUrl_StaysOnSafeLocalPage(string candidate, string expected) =>
        Assert.Equal(expected, CultureController.SafeReturnUrl(candidate));

    [Fact]
    public void LanguageSwitch_SetsPersistentCookieAndRejectsUnsupportedCulture()
    {
        var controller = new CultureController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result = controller.Change(WebsiteCultures.Badini, "/products?page=2");
        Assert.Equal("/products?page=2", Assert.IsType<LocalRedirectResult>(result).Url);
        var cookie = controller.Response.Headers.SetCookie.ToString();
        Assert.Contains(CookieRequestCultureProvider.DefaultCookieName, cookie);
        Assert.Contains("ku-Arab-IQ", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<BadRequestResult>(controller.Change("ckb-IQ", "/products"));
    }

    [Theory]
    [InlineData("12.50", 12.50)]
    [InlineData("١٢٫٥٠", 12.50)]
    [InlineData("۱۲٫۵۰", 12.50)]
    public void NumericParser_AcceptsUnambiguousPriceInAllScripts(string input, double expected)
    {
        Assert.True(WebsiteNumberParser.TryDecimal(input, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("1,234")]
    [InlineData("١٬٢٣٤")]
    [InlineData("1.2.3")]
    public void NumericParser_RejectsAmbiguousAmounts(string input) =>
        Assert.False(WebsiteNumberParser.TryDecimal(input, out _));

    [Theory]
    [InlineData(WebsiteCultures.English)]
    [InlineData(WebsiteCultures.Arabic)]
    [InlineData(WebsiteCultures.Badini)]
    public void FormattedDecimal_CanBeParsedWithoutChangingValue(string tag)
    {
        var displayed = 12.50m.ToString(CultureInfo.GetCultureInfo(tag));
        Assert.True(WebsiteNumberParser.TryDecimal(displayed, out var parsed));
        Assert.Equal(12.50m, parsed);
    }

    [Fact]
    public void NumericScript_DoesNotChangePricingCalculation()
    {
        Assert.True(WebsiteNumberParser.TryDecimal("12.50", out var ascii));
        Assert.True(WebsiteNumberParser.TryDecimal("١٢٫٥٠", out var arabic));
        Assert.True(WebsiteNumberParser.TryInt("۱۰", out var quantity));
        var calculator = new PricingSimulationCalculator();
        var a = calculator.Calculate(10m, ascii, 5m, 10, quantity);
        var b = calculator.Calculate(10m, arabic, 5m, 10, quantity);
        Assert.Equal(a, b);
    }

    private static void AssertComplete(Type marker, string baseName)
    {
        var manager = new ResourceManager(baseName, marker.Assembly);
        var english = Keys(manager, CultureInfo.InvariantCulture);
        Assert.NotEmpty(english);
        foreach (var tag in new[] { WebsiteCultures.Arabic, "ku" })
        {
            var translated = Keys(manager, CultureInfo.GetCultureInfo(tag));
            Assert.Equal(english.Order(), translated.Order());
        }
    }

    private static HashSet<string> Keys(ResourceManager manager, CultureInfo culture)
    {
        var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        return set.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).ToHashSet();
    }
}
