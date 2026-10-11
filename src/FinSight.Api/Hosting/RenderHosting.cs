namespace FinSight.Api.Hosting;

public static class RenderHosting
{
    public static Uri? Configure(WebApplicationBuilder builder)
    {
        var port = builder.Configuration["PORT"];
        if (!string.IsNullOrWhiteSpace(port))
        {
            if (!int.TryParse(port, out var number) || number is < 1024 or > 65535)
                throw new InvalidOperationException("PORT must be between 1024 and 65535.");
            builder.WebHost.UseUrls($"http://0.0.0.0:{number}");
        }
        if (builder.Configuration["Hosting:Platform"] != "Render") return null;
        if (builder.Configuration.GetValue<bool>("ASPNETCORE_FORWARDEDHEADERS_ENABLED"))
            throw new InvalidOperationException("Do not enable trust-all forwarded headers for the Render hosting profile.");
        return ValidateOrigin(builder.Configuration["Email:WebsiteBaseUrl"], builder.Configuration["AllowedHosts"]);
    }

    public static Uri ValidateOrigin(string? publicOrigin, string? allowedHosts)
    {
        if (!Uri.TryCreate(publicOrigin, UriKind.Absolute, out var origin)
            || origin.Scheme != "https" || !origin.IsDefaultPort || origin.AbsolutePath != "/"
            || origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0
            || !origin.Host.EndsWith(".onrender.com", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(allowedHosts, origin.Host, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Render requires Email:WebsiteBaseUrl=https://<actual-host>.onrender.com and AllowedHosts set to that exact hostname.");
        return origin;
    }

    public static Task NormalizeHttps(HttpContext context, RequestDelegate next, Uri origin)
    {
        if (!string.Equals(context.Request.Host.Host, origin.Host, StringComparison.OrdinalIgnoreCase)
            || context.Request.Host.Port is not (null or 443))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return Task.CompletedTask;
        }
        // Render terminates TLS and redirects all public HTTP traffic at its edge.
        // This explicit platform profile derives the external scheme from deployment
        // configuration, NOT arbitrary X-Forwarded-* headers or guessed proxy CIDRs.
        // Do not use it on a directly exposed HTTP server.
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.Request.Host = new HostString(origin.Host);
        return next(context);
    }
}
