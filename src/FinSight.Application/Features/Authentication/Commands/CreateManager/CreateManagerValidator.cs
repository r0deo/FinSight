using FluentValidation;

namespace FinSight.Application.Features.Authentication.Commands.CreateManager;

public sealed class CreateManagerValidator : AbstractValidator<CreateManagerCommand>
{
    public CreateManagerValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress();
        RuleFor(command => command.Password).NotEmpty().MinimumLength(8);
    }
}
