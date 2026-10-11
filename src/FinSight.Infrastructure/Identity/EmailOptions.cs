namespace FinSight.Infrastructure.Identity;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; init; } = true;

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 25;

    public string FromAddress { get; init; } = string.Empty;

    public string? Username { get; init; }

    public string? Password { get; init; }

    public bool EnableSsl { get; init; }

    public string WebsiteBaseUrl { get; init; } = string.Empty;
}
