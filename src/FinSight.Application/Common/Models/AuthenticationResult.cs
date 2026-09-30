namespace FinSight.Application.Common.Models;

public sealed record AuthenticationResult(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    Guid BusinessId,
    string Role);
