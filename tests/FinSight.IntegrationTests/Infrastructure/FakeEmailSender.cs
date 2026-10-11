using System.Collections.Concurrent;
using FinSight.Application.Common.Interfaces;

namespace FinSight.IntegrationTests.Infrastructure;

public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyCollection<EmailMessage> Messages => _messages.ToArray();
    public bool FailDelivery { get; set; }

    public Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (FailDelivery) throw new System.Net.Mail.SmtpException("Simulated delivery failure");
        _messages.Enqueue(new EmailMessage(recipient, subject, body));
        return Task.CompletedTask;
    }
}

public sealed record EmailMessage(string Recipient, string Subject, string Body)
{
    public Uri ConfirmationLink => new(System.Text.RegularExpressions.Regex.Match(Body, @"https://[^\s]+").Value);
    public string ConfirmationToken => Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(ConfirmationLink.Query)["token"].ToString();
}
