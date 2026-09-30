namespace FinSight.Api.Contracts.Authentication;

public sealed record AuthenticationResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    Guid UserId,
    Guid BusinessId,
    string Role);
