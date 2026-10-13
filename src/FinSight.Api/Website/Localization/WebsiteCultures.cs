using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace FinSight.Api.Website.Localization;

public static class WebsiteCultures
{
    public const string English = "en";
    public const string Arabic = "ar-IQ";
    // BCP-47 script/region tag; resources contain Badini/Kurmanji, not Sorani.
    public const string Badini = "ku-Arab-IQ";

    public static readonly string[] Supported = [English, Arabic, Badini];

    public static RequestLocalizationOptions Configure()
    {
        var cultures = Supported.Select(CultureInfo.GetCultureInfo).ToArray();
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(English),
            SupportedCultures = cultures,
            SupportedUICultures = cultures,
            FallBackToParentCultures = true,
            FallBackToParentUICultures = true,
            ApplyCurrentCultureToResponseHeaders = true
        };
        // A selected cookie should not be overridden by a URL query parameter.
        foreach (var provider in options.RequestCultureProviders.OfType<QueryStringRequestCultureProvider>().ToArray())
            options.RequestCultureProviders.Remove(provider);
        return options;
    }

    public static bool IsRightToLeft(CultureInfo culture) =>
        culture.Name is Arabic or Badini;

    public static bool IsSupported(string? culture) =>
        Supported.Contains(culture, StringComparer.OrdinalIgnoreCase);
}
