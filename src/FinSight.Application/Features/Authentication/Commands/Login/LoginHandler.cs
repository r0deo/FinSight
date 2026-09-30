using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.Login;

public sealed class LoginHandler(IAuthenticationService authenticationService)
    : IRequestHandler<LoginCommand, AuthenticationResult>
{
    public Task<AuthenticationResult> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        return authenticationService.LoginAsync(
            request.Email,
            request.Password,
            cancellationToken);
    }
}
