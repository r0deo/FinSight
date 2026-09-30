using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.CreateManager;

public sealed class CreateManagerHandler(
    IAuthenticationService authenticationService,
    ICurrentUserContext currentUserContext)
    : IRequestHandler<CreateManagerCommand, RegistrationResult>
{
    public Task<RegistrationResult> Handle(
        CreateManagerCommand request,
        CancellationToken cancellationToken)
    {
        return authenticationService.CreateManagerAsync(
            currentUserContext.BusinessId,
            request.Email,
            request.Password,
            cancellationToken);
    }
}
