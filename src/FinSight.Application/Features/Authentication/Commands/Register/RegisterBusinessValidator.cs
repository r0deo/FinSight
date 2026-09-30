using FinSight.Domain.Entities;
using FluentValidation;

namespace FinSight.Application.Features.Authentication.Commands.Register;

public sealed class RegisterBusinessValidator
    : AbstractValidator<RegisterBusinessCommand>
{
    public RegisterBusinessValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8);

        RuleFor(command => command.BusinessName)
            .NotEmpty()
            .MaximumLength(Business.MaxNameLength);

        RuleFor(command => command.Location)
            .NotEmpty()
            .MaximumLength(Business.MaxLocationLength);

        RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(Business.CurrencyLength);

        RuleFor(command => command.ContactInformation)
            .NotEmpty()
            .MaximumLength(Business.MaxContactInformationLength);
    }
}
