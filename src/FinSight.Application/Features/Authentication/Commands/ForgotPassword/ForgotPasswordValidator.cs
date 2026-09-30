using FluentValidation;

namespace FinSight.Application.Features.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
    }
}
