using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace FinSight.Api.Website;

// MVC's empty summary includes <li style="display:none">. These server-validated
// forms need no empty placeholder; omit it without allowing inline styles in CSP.
[HtmlTargetElement("div", Attributes = "asp-validation-summary")]
public sealed class ValidationSummaryCspTagHelper : TagHelper
{
    public override int Order => 1000;

    [ViewContext, HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.ModelState.IsValid) output.SuppressOutput();
    }
}
