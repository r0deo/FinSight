using FluentValidation;

namespace FinSight.Application.Features.Authentication.Commands.RefreshToken;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator()
    {
        RuleFor(command => command.RefreshToken).NotEmpty();
    }
}
