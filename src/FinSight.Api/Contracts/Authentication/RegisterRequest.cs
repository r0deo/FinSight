namespace FinSight.Api.Contracts.Authentication;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string BusinessName,
    string Location,
    string Currency,
    string ContactInformation);
