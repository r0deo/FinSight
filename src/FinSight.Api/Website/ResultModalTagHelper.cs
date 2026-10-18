using FinSight.Api.Website.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Localization;

namespace FinSight.Api.Website;

// Presentation only: progressively enhance the existing result and its explicit actions.
[HtmlTargetElement("result-modal")]
public sealed class ResultModalTagHelper(IStringLocalizer<RebazText> text) : TagHelper
{
    public string Id { get; set; } = "calculation-result";
    public string Title { get; set; } = "";
    public string SourceForm { get; set; } = "";
    public string ReturnFocus { get; set; } = "";
    public bool AutoOpen { get; set; } = true;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = await output.GetChildContentAsync();
        output.TagName = "div";
        output.Attributes.Clear();
        output.Attributes.SetAttribute("data-result-shell", "");
        var reopen = new TagBuilder("button");
        reopen.Attributes["type"] = "button";
        reopen.Attributes["class"] = "button button-quiet result-reopen";
        reopen.Attributes["data-result-open"] = Id;
        reopen.Attributes["aria-controls"] = Id;
        reopen.Attributes["aria-haspopup"] = "dialog";
        reopen.Attributes["hidden"] = "hidden";
        reopen.InnerHtml.Append(text["View calculation results"]);

        // Without JavaScript this remains a single inline result with working save/apply forms.
        var result = new TagBuilder("section");
        result.Attributes["id"] = Id;
        result.Attributes["class"] = "result-modal";
        result.Attributes["data-result-modal"] = "";
        result.Attributes["data-source-form"] = SourceForm;
        result.Attributes["data-return-focus"] = ReturnFocus;
        result.Attributes["data-auto-open"] = AutoOpen ? "true" : "false";
        result.Attributes["aria-labelledby"] = Id + "-title";
        var header = new TagBuilder("header");
        header.Attributes["class"] = "result-modal-header";
        var title = new TagBuilder("h2");
        title.Attributes["id"] = Id + "-title";
        title.InnerHtml.Append(Title);
        var close = new TagBuilder("button");
        close.Attributes["type"] = "button";
        close.Attributes["class"] = "button button-quiet button-small";
        close.Attributes["data-result-close"] = "";
        close.Attributes["hidden"] = "hidden";
        close.InnerHtml.Append(text["Close results"]);
        header.InnerHtml.AppendHtml(title).AppendHtml(close);
        var body = new TagBuilder("div");
        body.Attributes["class"] = "result-modal-body";
        body.InnerHtml.AppendHtml(content);
        result.InnerHtml.AppendHtml(header).AppendHtml(body);
        output.Content.AppendHtml(reopen).AppendHtml(result);
    }
}
