namespace FinSight.Api.Contracts.Authentication;

public sealed record RegistrationResponse(
    Guid UserId,
    Guid BusinessId,
    string Role,
    bool RequiresEmailConfirmation);
