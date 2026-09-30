using FinSight.Application.Common.Interfaces;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.RevokeToken;

public sealed class RevokeTokenHandler(
    IAuthenticationService authenticationService,
    ICurrentUserContext currentUserContext) : IRequestHandler<RevokeTokenCommand>
{
    public Task Handle(
        RevokeTokenCommand request,
        CancellationToken cancellationToken)
    {
        return authenticationService.RevokeRefreshTokenAsync(
            currentUserContext.UserId,
            request.RefreshToken,
            cancellationToken);
    }
}
