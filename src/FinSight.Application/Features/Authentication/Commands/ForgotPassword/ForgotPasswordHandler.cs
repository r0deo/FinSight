using FinSight.Application.Common.Interfaces;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordHandler(IAuthenticationService authenticationService)
    : IRequestHandler<ForgotPasswordCommand>
{
    public Task Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        return authenticationService.RequestPasswordResetAsync(
            request.Email,
            cancellationToken);
    }
}
