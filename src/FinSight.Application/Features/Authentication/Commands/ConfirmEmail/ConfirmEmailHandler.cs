using FinSight.Application.Common.Interfaces;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.ConfirmEmail;

public sealed class ConfirmEmailHandler(IAuthenticationService authenticationService)
    : IRequestHandler<ConfirmEmailCommand>
{
    public Task Handle(
        ConfirmEmailCommand request,
        CancellationToken cancellationToken)
    {
        return authenticationService.ConfirmEmailAsync(
            request.UserId,
            request.Token,
            cancellationToken);
    }
}
