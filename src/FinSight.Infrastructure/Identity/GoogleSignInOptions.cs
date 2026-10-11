namespace FinSight.Infrastructure.Identity;

public sealed class GoogleSignInOptions
{
    public const string SectionName = "Authentication:Google";
    public const string VerifiedEmailClaim = "urn:finsight:google:email_verified";
    public bool Enabled { get; init; }
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}
