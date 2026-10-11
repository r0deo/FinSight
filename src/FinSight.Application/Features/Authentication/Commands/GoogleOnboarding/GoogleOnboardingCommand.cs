using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FluentValidation;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.GoogleOnboarding;

public sealed record GoogleOnboardingCommand(string BusinessName, string Location, string Currency,
    string ContactInformation) : IRequest<GoogleSignInOutcome>;

public sealed class GoogleOnboardingValidator : AbstractValidator<GoogleOnboardingCommand>
{
    public GoogleOnboardingValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(Business.MaxNameLength);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(Business.MaxLocationLength);
        RuleFor(x => x.Currency).NotEmpty().Length(Business.CurrencyLength);
        RuleFor(x => x.ContactInformation).NotEmpty().MaximumLength(Business.MaxContactInformationLength);
    }
}

public sealed class GoogleOnboardingHandler(IGoogleSignInService google) : IRequestHandler<GoogleOnboardingCommand, GoogleSignInOutcome>
{
    public Task<GoogleSignInOutcome> Handle(GoogleOnboardingCommand request, CancellationToken cancellationToken) =>
        google.CreateBusinessAsync(new Business(Guid.NewGuid(), request.BusinessName, request.Location,
            request.Currency, request.ContactInformation), cancellationToken);
}
