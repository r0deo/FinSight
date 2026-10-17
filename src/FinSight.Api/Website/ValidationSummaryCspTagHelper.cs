using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using FinSight.Api.Website.Localization;
using Microsoft.Extensions.Localization;

namespace FinSight.Api.Website;

// MVC's empty summary includes <li style="display:none">. These server-validated
// forms need no empty placeholder; omit it without allowing inline styles in CSP.
[HtmlTargetElement("div", Attributes = "asp-validation-summary")]
public sealed class ValidationSummaryCspTagHelper(IStringLocalizer<RebazText> text) : TagHelper
{
    public override int Order => 1000;

    [HtmlAttributeName("asp-validation-summary")]
    public ValidationSummary Mode { get; set; } = ValidationSummary.All;

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var state = ViewContext.ModelState;
        if (Mode == ValidationSummary.None || state.IsValid)
        { output.SuppressOutput(); return; }

        var errors = state.Where(x => Mode == ValidationSummary.All || x.Key == string.Empty)
            .SelectMany(x => x.Value!.Errors).ToArray();
        if (Mode == ValidationSummary.ModelOnly && errors.Length == 0)
        { output.SuppressOutput(); return; }

        const string fallbackKey = "Please check the supplied values and try again.";
        var fallback = text[fallbackKey].Value;
        if (string.IsNullOrWhiteSpace(fallback)) fallback = fallbackKey;
        // Do not render MVC's empty, inline-style placeholder or expose exception details.
        var messages = errors.Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? fallback : e.ErrorMessage)
            .Distinct().ToArray();
        if (messages.Length == 0) messages = [fallback];
        var list = new TagBuilder("ul");
        foreach (var message in messages)
        {
            var item = new TagBuilder("li");
            item.InnerHtml.Append(message); // HTML-encode even unexpected messages.
            list.InnerHtml.AppendHtml(item);
        }
        // MVC appends its generated list to PostContent; replace it, do not duplicate it.
        output.PostContent.Clear();
        output.Content.SetHtmlContent(list);
    }
}
