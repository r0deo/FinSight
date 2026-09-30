using FinSight.Application.Common.Models;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password) : IRequest<AuthenticationResult>;
