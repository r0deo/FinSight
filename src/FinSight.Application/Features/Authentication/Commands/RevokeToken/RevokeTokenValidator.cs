using FluentValidation;

namespace FinSight.Application.Features.Authentication.Commands.RevokeToken;

public sealed class RevokeTokenValidator : AbstractValidator<RevokeTokenCommand>
{
    public RevokeTokenValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}
