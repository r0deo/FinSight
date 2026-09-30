using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.RevokeToken;

public sealed record RevokeTokenCommand(string RefreshToken) : IRequest;
