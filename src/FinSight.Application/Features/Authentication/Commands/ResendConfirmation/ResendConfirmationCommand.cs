using FinSight.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.ResendConfirmation;

public sealed record ResendConfirmationCommand(string Email) : IRequest;

public sealed class ResendConfirmationValidator : AbstractValidator<ResendConfirmationCommand>
{
    public ResendConfirmationValidator() => RuleFor(command => command.Email).NotEmpty().EmailAddress();
}

public sealed class ResendConfirmationHandler(IEmailConfirmationService confirmation) : IRequestHandler<ResendConfirmationCommand>
{
    public Task Handle(ResendConfirmationCommand request, CancellationToken cancellationToken) =>
        confirmation.ResendAsync(request.Email, cancellationToken);
}
