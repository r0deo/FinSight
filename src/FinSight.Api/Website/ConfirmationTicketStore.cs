using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace FinSight.Api.Website;

// Keep the Identity token out of rendered HTML and clean the URL before showing a form.
public sealed class ConfirmationTicketStore(IDataProtectionProvider protection)
{
    private const string CookieName = "__Host-FinSight.Confirm";
    private readonly ITimeLimitedDataProtector _protector = protection
        .CreateProtector("FinSight.ConfirmationForm.v1").ToTimeLimitedDataProtector();
    public void Set(HttpContext context, Guid userId, string token) => context.Response.Cookies.Append(CookieName,
        _protector.Protect(JsonSerializer.Serialize(new ConfirmationTicket(userId, token)), TimeSpan.FromMinutes(15)),
        new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/", MaxAge = TimeSpan.FromMinutes(15), IsEssential = true });
    public ConfirmationTicket? Get(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var value)) return null;
        try { return JsonSerializer.Deserialize<ConfirmationTicket>(_protector.Unprotect(value)); }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException) { return null; }
    }
    public void Clear(HttpContext context) => context.Response.Cookies.Delete(CookieName,
        new CookieOptions { Secure = true, HttpOnly = true, Path = "/" });
}

public sealed record ConfirmationTicket(Guid UserId, string Token);
