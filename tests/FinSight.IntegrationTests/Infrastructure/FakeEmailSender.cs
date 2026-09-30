using System.Collections.Concurrent;
using FinSight.Application.Common.Interfaces;

namespace FinSight.IntegrationTests.Infrastructure;

public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyCollection<EmailMessage> Messages => _messages.ToArray();

    public Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(new EmailMessage(recipient, subject, body));
        return Task.CompletedTask;
    }
}

public sealed record EmailMessage(string Recipient, string Subject, string Body);
