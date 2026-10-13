using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FinSight.Api.ExceptionHandling;
using FinSight.Application;
using FinSight.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using FinSight.Api.Website;
using FinSight.Infrastructure.Identity;
using FinSight.Api.Hosting;
using FinSight.Api.Website.Localization;
using Microsoft.Extensions.Localization;

var builder = WebApplication.CreateBuilder(args);
var renderOrigin = RenderHosting.Configure(builder);
var authenticationPermitLimit = builder.Configuration.GetValue(
    "RateLimiting:AuthenticationPermitLimit",
    10);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options => options.DataAnnotationLocalizerProvider =
        (_, factory) => factory.Create(typeof(RebazText)))
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton<
    IAuthorizationMiddlewareResultHandler,
    ProblemDetailsAuthorizationMiddlewareResultHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<WebsiteExceptionFilter>();
builder.Services.AddScoped<ConfirmationTicketStore>();
builder.Services.AddFinSightDataProtection(builder.Configuration, builder.Environment);
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-FinSight.Antiforgery";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("confirmation-resend", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3, Window = TimeSpan.FromMinutes(15), QueueLimit = 0
        }));
    options.AddPolicy("authentication", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authenticationPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, requireVerifiedTls: !builder.Environment.IsDevelopment());
builder.Services.AddAuthorization(options => options.AddPolicy("Website", policy =>
    policy.AddAuthenticationSchemes(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme)
        .RequireAuthenticatedUser()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
if (renderOrigin is not null)
    app.Use((context, next) => RenderHosting.NormalizeHttps(context, next, renderOrigin));
app.UseRequestLocalization(WebsiteCultures.Configure());
// OAuth must be reached at the trusted origin so authorization and code-exchange redirect URIs agree.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/account/google") || context.Request.Path == "/signin-google")
    {
        var origin = new Uri(builder.Configuration["Email:WebsiteBaseUrl"]!);
        if (!context.Request.IsHttps || !string.Equals(context.Request.Host.Value, origin.Authority, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var text = context.RequestServices.GetRequiredService<IStringLocalizer<RebazText>>();
            await context.Response.WriteAsync(text["Use the configured HTTPS website address to sign in with Google."]);
            return;
        }
    }
    await next();
});
app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseStaticFiles();
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api")
    && !context.Request.Path.StartsWithSegments("/openapi"), website =>
{
    website.UseStatusCodePagesWithReExecute("/error/{0}");
    website.Use(async (context, next) =>
    {
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; img-src 'self' data:; form-action 'self' https://accounts.google.com; frame-ancestors 'none'; base-uri 'self'";
        await next();
    });
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Liveness only: no secrets or database query on every probe (Neon may sleep).
app.MapGet("/health", () => Results.Text("ok", "text/plain")).AllowAnonymous();

app.Run();

public partial class Program;
