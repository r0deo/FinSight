using FinSight.Api.Contracts.Authentication;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Common.Models;
using FinSight.Application.Features.Authentication.Commands.ConfirmEmail;
using FinSight.Application.Features.Authentication.Commands.CreateManager;
using FinSight.Application.Features.Authentication.Commands.ForgotPassword;
using FinSight.Application.Features.Authentication.Commands.Login;
using FinSight.Application.Features.Authentication.Commands.RefreshToken;
using FinSight.Application.Features.Authentication.Commands.Register;
using FinSight.Application.Features.Authentication.Commands.ResetPassword;
using FinSight.Application.Features.Authentication.Commands.RevokeToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;

namespace FinSight.Api.Controllers;

[ApiController]
[EnableRateLimiting("authentication")]
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<RegistrationResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RegisterBusinessCommand(
            request.Email, request.Password, request.BusinessName,
            request.Location, request.Currency, request.ContactInformation), cancellationToken);
        return Ok(ToResponse(result));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new LoginCommand(request.Email, request.Password), cancellationToken);
        return Ok(ToResponse(result));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthenticationResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RefreshTokenCommand(request.RefreshToken), cancellationToken);
        return Ok(ToResponse(result));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new RevokeTokenCommand(request.RefreshToken), cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        ConfirmEmailRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ConfirmEmailCommand(request.UserId, request.Token), cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ForgotPasswordCommand(request.Email), cancellationToken);
        return Accepted();
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ResetPasswordCommand(
            request.UserId, request.Token, request.NewPassword), cancellationToken);
        return NoContent();
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = AppRoles.Owner)]
    [HttpPost("managers")]
    public async Task<ActionResult<RegistrationResponse>> CreateManager(
        CreateManagerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateManagerCommand(request.Email, request.Password), cancellationToken);
        return Ok(ToResponse(result));
    }

    private static AuthenticationResponse ToResponse(AuthenticationResult result) => new(
        result.AccessToken,
        result.ExpiresAtUtc,
        result.RefreshToken,
        result.RefreshTokenExpiresAtUtc,
        result.UserId,
        result.BusinessId,
        result.Role);

    private static RegistrationResponse ToResponse(RegistrationResult result) => new(
        result.UserId,
        result.BusinessId,
        result.Role,
        result.RequiresEmailConfirmation);
}
