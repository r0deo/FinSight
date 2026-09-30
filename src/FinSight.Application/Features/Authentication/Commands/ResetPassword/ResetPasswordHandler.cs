using FinSight.Application.Common.Interfaces;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordHandler(IAuthenticationService authenticationService)
    : IRequestHandler<ResetPasswordCommand>
{
    public Task Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        return authenticationService.ResetPasswordAsync(
            request.UserId,
            request.Token,
            request.NewPassword,
            cancellationToken);
    }
}
