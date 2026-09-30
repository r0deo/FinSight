using FinSight.Application.Common.Models;
using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.RefreshToken;

public sealed record RefreshTokenCommand(string RefreshToken)
    : IRequest<AuthenticationResult>;
