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
        if (!options.Enabled) throw new SmtpException("Email delivery has not been enabled.");
        using var message = new MailMessage(options.FromAddress, recipient, subject, body);
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(options.Username))
        {
            client.Credentials = new NetworkCredential(
                options.Username,
                options.Password);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try { await client.SendMailAsync(message, timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("SMTP delivery timed out.");
        }
    }
}
