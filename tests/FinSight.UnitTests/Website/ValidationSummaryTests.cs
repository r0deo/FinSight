using System.Globalization;
using System.Net;
using FinSight.Api.Website;
using FinSight.Api.Website.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FinSight.UnitTests.Website;

public class ValidationSummaryTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ar-IQ")]
    public void InvalidStateWithoutMessages_RendersLocalizedFallbackInsteadOfEmptyBullet(string culture)
    {
        var state = new ModelStateDictionary();
        state.AddModelError("Name", "Required");
        state["Name"]!.Errors.Clear(); // Regression: clearing messages does not make MVC state valid.
        Assert.False(state.IsValid);

        var html = Render(state, culture).Content.GetContent();

        Assert.Contains("<li>", html);
        Assert.DoesNotContain("<li></li>", html);
        Assert.DoesNotContain("style=", html);
        if (culture == "en") Assert.Contains("Please check the supplied values and try again.", html);
        else Assert.Matches(@"[\u0600-\u06ff]", WebUtility.HtmlDecode(html));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar-IQ")]
    public void BlankAndExceptionOnlyErrors_UseSafeNonemptyFallbackWithoutExceptionDetails(string culture)
    {
        var state = new ModelStateDictionary();
        state.AddModelError("CostHelper.PackagingPerUnit", " ");
        state.AddModelError("CostHelper.UsableOutput", new InvalidOperationException("private diagnostic data"),
            new EmptyModelMetadataProvider().GetMetadataForType(typeof(decimal)));

        var html = Render(state, culture).Content.GetContent();

        Assert.DoesNotContain("private diagnostic data", html);
        Assert.DoesNotContain("<li> </li>", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<li>"));
        Assert.False(string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", ""))));
    }

    [Fact]
    public void ErrorMessage_IsHtmlEncoded()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("Name", "<script>alert('test')</script>");
        var html = Render(state, "en").Content.GetContent();
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void ValidStateAndModelOnlyWithoutGlobalErrors_SuppressSummary()
    {
        Assert.Null(Render(new(), "en").TagName);
        var state = new ModelStateDictionary();
        state.AddModelError("Name", "Required");
        Assert.Null(Render(state, "en", ValidationSummary.ModelOnly).TagName);
        Assert.Null(Render(state, "en", ValidationSummary.None).TagName);
        state.AddModelError("", "Global error");
        var html = Render(state, "en", ValidationSummary.ModelOnly).Content.GetContent();
        Assert.Contains("Global error", html);
        Assert.DoesNotContain("Required", html);
    }

    private static TagHelperOutput Render(ModelStateDictionary state, string culture, ValidationSummary mode = ValidationSummary.All)
    {
        using var services = new ServiceCollection().AddLogging()
            .AddLocalization(options => options.ResourcesPath = "Resources").BuildServiceProvider();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var helper = new ValidationSummaryCspTagHelper(services.GetRequiredService<IStringLocalizer<RebazText>>())
            {
                Mode = mode,
                ViewContext = new ViewContext { ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), state) }
            };
            helper.ViewContext.ModelState.Merge(state);
            var output = new TagHelperOutput("div", new(), (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
            output.PostContent.SetHtmlContent("<ul><li style=\"display:none\"></li></ul>"); // MVC's generated placeholder.
            helper.Process(new TagHelperContext(new(), new Dictionary<object, object>(), "summary"), output);
            Assert.Empty(output.PostContent.GetContent());
            return output;
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
