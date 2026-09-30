namespace FinSight.Api.Contracts.Authentication;

public sealed record ConfirmEmailRequest(Guid UserId, string Token);
