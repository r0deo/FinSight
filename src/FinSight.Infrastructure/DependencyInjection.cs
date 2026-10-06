using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Common.Interfaces;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using FinSight.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

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
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host),
                "Email host is required.")
            .Validate(options => options.Port > 0,
                "Email port must be greater than zero.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.FromAddress),
                "Email sender address is required.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

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
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IPricingSimulationRepository, PricingSimulationRepository>();

        return services;
    }
}
