namespace FinSight.Application.Common.Models;

public sealed record RegistrationResult(
    Guid UserId,
    Guid BusinessId,
    string Role,
    bool RequiresEmailConfirmation);
