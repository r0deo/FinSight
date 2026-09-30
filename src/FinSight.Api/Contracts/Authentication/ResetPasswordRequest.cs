namespace FinSight.Api.Contracts.Authentication;

public sealed record ResetPasswordRequest(
    Guid UserId,
    string Token,
    string NewPassword);
