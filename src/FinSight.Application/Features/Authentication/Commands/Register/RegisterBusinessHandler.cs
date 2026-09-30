using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Domain.Entities;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.Register;

public sealed class RegisterBusinessHandler(
    IAuthenticationService authenticationService)
    : IRequestHandler<RegisterBusinessCommand, RegistrationResult>
{
    public Task<RegistrationResult> Handle(
        RegisterBusinessCommand request,
        CancellationToken cancellationToken)
    {
        var business = new Business(
            Guid.NewGuid(),
            request.BusinessName,
            request.Location,
            request.Currency,
            request.ContactInformation);

        return authenticationService.RegisterOwnerAsync(
            business,
            request.Email,
            request.Password,
            cancellationToken);
    }
}
