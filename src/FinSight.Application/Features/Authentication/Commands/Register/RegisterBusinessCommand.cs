using FinSight.Application.Common.Models;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.Register;

public sealed record RegisterBusinessCommand(
    string Email,
    string Password,
    string BusinessName,
    string Location,
    string Currency,
    string ContactInformation) : IRequest<RegistrationResult>;
