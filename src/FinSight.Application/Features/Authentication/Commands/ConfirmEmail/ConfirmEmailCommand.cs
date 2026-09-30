using MediatR;

namespace FinSight.Application.Features.Authentication.Commands.ConfirmEmail;

public sealed record ConfirmEmailCommand(Guid UserId, string Token) : IRequest;
