using FinSight.Api.Website.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Website.Controllers;

public sealed class CultureController : WebsiteController
{
    [AllowAnonymous, HttpPost("/culture")]
    public IActionResult Change(string culture, string? returnUrl)
    {
        if (!WebsiteCultures.IsSupported(culture)) return BadRequest();
        var target = SafeReturnUrl(returnUrl);
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
                IsEssential = true, Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
        return LocalRedirect(target);
    }

    public static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/')
            || returnUrl.StartsWith("//") || returnUrl.Contains('\\')
            || returnUrl.Any(char.IsControl)) return "/";
        var path = returnUrl.Split('?', '#')[0];
        if (path.Contains('%')) return "/";
        if (path is "/" or "/home" or "/products" or "/products/create" or
            "/what-if" or "/what-if/history" or "/business-costs" or "/business-costs/create" or
            "/simulations/preview" or "/simulations" or "/account/login" or "/account/register" or
            "/account/check-email" or "/account/confirm-email" or
            "/account/confirmation-success" or "/account/resend-confirmation" or
            "/account/resend-sent" or "/account/settings" or "/account/access-denied" or
            "/account/google/business" or "/account/google/error" or
            "/account/google/existing-account" or "/account/google/link") return returnUrl;
        var segments = path.Split('/');
        if (segments.Length == 4 && segments[1] == "business-costs"
            && Guid.TryParse(segments[2], out _) && segments[3] is "edit" or "delete") return returnUrl;
        if (segments.Length == 4 && segments[1] == "products"
            && Guid.TryParse(segments[2], out _) && segments[3] == "edit") return returnUrl;
        if (segments.Length == 3 && segments[1] == "simulations"
            && Guid.TryParse(segments[2], out _)) return returnUrl;
        if (segments.Length == 3 && segments[1] == "what-if"
            && Guid.TryParse(segments[2], out _)) return returnUrl;
        return "/";
    }
}
