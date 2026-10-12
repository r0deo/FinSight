namespace FinSight.Api.Website;

public static class PostAuthenticationDestination
{
    public const string Home = "/home";
    public const string Onboarding = "/account/google/business";

    public static string Resolve(bool needsBusinessSetup, string? returnUrl = null)
    {
        if (needsBusinessSetup) return Onboarding;
        // Only resumable website pages. Never replay auth callbacks, POST actions,
        // API requests, or onboarding. Reject encoded paths instead of normalizing
        // potentially ambiguous URLs differently from the browser/router.
        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/')
            || returnUrl.StartsWith("//") || returnUrl.Contains('\\')
            || returnUrl.Any(char.IsControl)) return Home;
        var path = returnUrl.Split('?', '#')[0];
        if (path.Contains('%')) return Home;
        if (path == Home || path == "/products" || path == "/products/create"
            || path == "/account/settings" || path == "/simulations/preview") return returnUrl;
        var segments = path.Split('/');
        return segments.Length == 4 && segments[1] == "products"
            && Guid.TryParse(segments[2], out _) && segments[3] == "edit" ? returnUrl : Home;
    }
}
