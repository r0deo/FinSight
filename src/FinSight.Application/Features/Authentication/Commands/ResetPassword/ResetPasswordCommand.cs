using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.ResetPassword;

public sealed record ResetPasswordCommand(
    Guid UserId,
    string Token,
    string NewPassword) : IRequest;
