using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Common.Interfaces;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using FinSight.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinSight.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration, bool requireVerifiedTls = false)
    {
        var connectionString = PostgresConfiguration.ConnectionString(configuration, requireVerifiedTls);

        services.AddDbContext<FinSightDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<FinSightDbContext>()
            .AddDefaultTokenProviders()
            .AddClaimsPrincipalFactory<WebsiteClaimsPrincipalFactory>()
            .AddSignInManager();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer),
                "JWT issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience),
                "JWT audience is required.")
            .Validate(options => options.SigningKey.Length >= 32,
                "JWT signing key must be at least 32 characters.")
            .Validate(options => options.ExpirationMinutes > 0,
                "JWT expiration must be greater than zero.")
            .Validate(options => options.RefreshTokenExpirationDays > 0,
                "Refresh-token expiration must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Host),
                "Email host is required.")
            .Validate(options => !options.Enabled || options.Port > 0,
                "Email port must be greater than zero.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.FromAddress),
                "Email sender address is required.")
            .Validate(options => Uri.TryCreate(options.WebsiteBaseUrl, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps,
                "Email website base URL must be an absolute HTTPS URL.")
            .ValidateOnStart();

        services.AddAuthentication("FinSight")
            .AddPolicyScheme("FinSight", "Website or API", options =>
                options.ForwardDefaultSelector = context =>
                    context.Request.Path.StartsWithSegments("/api")
                    || context.Request.Path.StartsWithSegments("/openapi")
                        ? JwtBearerDefaults.AuthenticationScheme
                        : IdentityConstants.ApplicationScheme)
            .AddJwtBearer()
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "__Host-FinSight.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.LoginPath = "/account/login";
                options.AccessDeniedPath = "/account/access-denied";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync;
            });

        services.AddOptions<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, configuredOptions) =>
            {
                var jwt = configuredOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = CustomClaimTypes.Role
                };
            });

        services.AddAuthorization();
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(24));
        services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>();
        services.AddFinSightGoogle(configuration);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IEmailConfirmationService, EmailConfirmationService>();
        services.AddScoped<IGoogleSignInService, GoogleSignInService>();
        services.AddScoped<IAccountConnections, AccountConnections>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IWebsiteAuthenticationService, WebsiteAuthenticationService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IPricingSimulationRepository, PricingSimulationRepository>();

        return services;
    }
}
