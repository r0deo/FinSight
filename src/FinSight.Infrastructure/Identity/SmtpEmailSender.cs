using System.Net;
using System.Net.Mail;
using FinSight.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace FinSight.Infrastructure.Identity;

public sealed class SmtpEmailSender(IOptions<EmailOptions> emailOptions)
    : IEmailSender
{
    public async Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var options = emailOptions.Value;
        using var message = new MailMessage(options.FromAddress, recipient, subject, body);
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            client.Credentials = new NetworkCredential(
                options.Username,
                options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
