using System.Globalization;
using System.Net;
using FinSight.Api.Website;
using FinSight.Api.Website.Localization;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FinSight.UnitTests.Website;

public class ResultModalTests
{
    [Theory]
    [InlineData("en", true)]
    [InlineData("ar-IQ", true)]
    [InlineData("ku-Arab-IQ", false)]
    public async Task SharedModal_EncodesTitleLocalizesControlsAndPreservesExplicitActions(string culture, bool autoOpen)
    {
        using var services = new ServiceCollection().AddLogging().AddLocalization(o => o.ResourcesPath = "Resources").BuildServiceProvider();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var text = services.GetRequiredService<IStringLocalizer<RebazText>>();
            var helper = new ResultModalTagHelper(text)
            {
                Id = "test-result", Title = "<script>unsafe</script>", SourceForm = "#form",
                ReturnFocus = "#calculate", AutoOpen = autoOpen
            };
            const string original = "<form method=\"post\" action=\"/explicit-save\"><button type=\"submit\">Save</button></form>";
            var output = new TagHelperOutput("result-modal", new() { { "id", "test-result" } },
                (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(original)));
            await helper.ProcessAsync(new TagHelperContext(new(), new Dictionary<object, object>(), "result"), output);
            var html = output.Content.GetContent();
            Assert.Equal("div", output.TagName);
            Assert.False(output.Attributes.ContainsName("id")); // No duplicate id on the wrapper.
            Assert.Contains("aria-labelledby=\"test-result-title\"", html);
            Assert.Contains($"data-auto-open=\"{autoOpen.ToString().ToLowerInvariant()}\"", html);
            Assert.Contains("data-source-form=\"#form\"", html);
            Assert.Contains("data-return-focus=\"#calculate\"", html);
            Assert.DoesNotContain("<script>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains(original, html); // Result/save content is not duplicated, rewritten or submitted.
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "action=\"/explicit-save\""));
            Assert.Contains(text["Close results"].Value, WebUtility.HtmlDecode(html));
            Assert.Contains(text["View calculation results"].Value, WebUtility.HtmlDecode(html));
            if (culture != "en") Assert.NotEqual("Close results", text["Close results"].Value);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "type=\"button\"").Count);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
