using System.Net.Mail;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FinSight.Infrastructure.Identity;

public sealed class EmailOptionsValidator(IHostEnvironment environment) : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        if (!Uri.TryCreate(options.WebsiteBaseUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.AbsolutePath != "/"
            || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
            return ValidateOptionsResult.Fail("Email:WebsiteBaseUrl must be a trusted HTTPS origin, without a path, credentials, query or fragment.");
        if (!environment.IsDevelopment() && uri.IsLoopback)
            return ValidateOptionsResult.Fail("Production requires a public HTTPS website origin.");
        if (!options.Enabled) return ValidateOptionsResult.Success;
        if (!MailAddress.TryCreate(options.FromAddress, out _))
            return ValidateOptionsResult.Fail("Email:FromAddress must be a valid sender address.");
        if (options.Port is < 1 or > 65535 || string.IsNullOrWhiteSpace(options.Host))
            return ValidateOptionsResult.Fail("A valid SMTP host and port are required.");
        if (!environment.IsDevelopment() && (!options.EnableSsl
            || string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password)
            || uri.IsLoopback || options.Host is "localhost" or "mailpit"))
            return ValidateOptionsResult.Fail("Production email requires authenticated SMTP with STARTTLS and a public HTTPS website origin.");
        return ValidateOptionsResult.Success;
    }
}
