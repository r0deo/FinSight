using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.RefreshToken;

public sealed class RefreshTokenHandler(IAuthenticationService authenticationService)
    : IRequestHandler<RefreshTokenCommand, AuthenticationResult>
{
    public Task<AuthenticationResult> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        return authenticationService.RefreshAsync(
            request.RefreshToken,
            cancellationToken);
    }
}
