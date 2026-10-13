using System.Net.Mail;
using FinSight.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;

namespace FinSight.Infrastructure.Identity;

public sealed class EmailConfirmationService(
    UserManager<ApplicationUser> users,
    IEmailSender sender,
    IOptions<EmailOptions> options,
    ILogger<EmailConfirmationService> logger,
    IStringLocalizer<IdentityEmailText> text) : IEmailConfirmationService
{
    public async Task ResendAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is not null && !user.EmailConfirmed)
            await SendAsync(user.Id, cancellationToken);
        // Callers always return the same response for missing, confirmed, and unconfirmed accounts.
    }

    public async Task SendAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || user.EmailConfirmed) return;
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var url = $"{options.Value.WebsiteBaseUrl.TrimEnd('/')}/account/confirm-email"
            + $"?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        try
        {
            await sender.SendAsync(user.Email!, text["ConfirmationSubject"],
                text["ConfirmationBody", url], cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or IOException or TimeoutException)
        {
            // The account is already committed. Do not log SMTP replies, recipients, links, or tokens.
            logger.LogWarning("Confirmation email delivery failed. The account is retained and can request a resend.");
        }
    }
}
