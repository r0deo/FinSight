using FinSight.Application.Common.Models;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.CreateManager;

public sealed record CreateManagerCommand(
    string Email,
    string Password) : IRequest<RegistrationResult>;
