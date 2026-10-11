using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Infrastructure.Identity;

public static class GoogleAuthenticationRegistration
{
    public static IServiceCollection AddFinSightGoogle(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GoogleSignInOptions>(configuration.GetSection(GoogleSignInOptions.SectionName));
        var settings = configuration.GetSection(GoogleSignInOptions.SectionName).Get<GoogleSignInOptions>() ?? new();
        services.AddAuthentication().AddCookie(IdentityConstants.ExternalScheme, options =>
        {
            options.Cookie.Name = "__Host-FinSight.External";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        });
        if (!settings.Enabled) return services;
        if (string.IsNullOrWhiteSpace(settings.ClientId) || string.IsNullOrWhiteSpace(settings.ClientSecret))
            throw new InvalidOperationException("Google sign-in is enabled but its client ID or client secret is missing.");
        services.AddAuthentication().AddGoogle(options =>
        {
            options.ClientId = settings.ClientId;
            options.ClientSecret = settings.ClientSecret;
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.CallbackPath = "/signin-google";
            options.UsePkce = true;
            options.SaveTokens = false;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("email");
            options.Scope.Add("profile");
            options.UserInformationEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
            options.ClaimActions.MapJsonKey(GoogleSignInOptions.VerifiedEmailClaim, "email_verified");
            options.Events.OnRemoteFailure = async context =>
            {
                context.HandleResponse();
                await context.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                context.Response.Redirect("/account/google/error");
            };
        });
        return services;
    }
}
