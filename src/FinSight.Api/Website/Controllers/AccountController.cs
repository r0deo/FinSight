using FinSight.Api.Website.Models;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Authentication.Commands.ConfirmEmail;
using FinSight.Application.Features.Authentication.Commands.Login;
using FinSight.Application.Features.Authentication.Commands.Register;
using FinSight.Application.Features.Authentication.Commands.ResendConfirmation;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FinSight.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace FinSight.Api.Website.Controllers;

[Route("account")]
public sealed class AccountController(
    ISender sender,
    IWebsiteAuthenticationService authentication,
    IValidator<LoginCommand> loginValidator,
    ConfirmationTicketStore confirmationTickets,
    IOptions<EmailOptions> emailOptions,
    IAccountConnections connections) : WebsiteController
{
    [AllowAnonymous, HttpGet("register")]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true
        ? LocalRedirect(PostAuthenticationDestination.Home) : View(new RegisterViewModel());

    [AllowAnonymous, HttpPost("register"), EnableRateLimiting("authentication")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(PostAuthenticationDestination.Home);
        if (!emailOptions.Value.Enabled)
        {
            ModelState.AddModelError(string.Empty, Text("Email registration is temporarily unavailable. Please use Google registration."));
            return InvalidForm("Register", model);
        }
        if (!ModelState.IsValid) return InvalidForm("Register", model);
        try
        {
            await sender.Send(new RegisterBusinessCommand(model.Email, model.Password,
                model.BusinessName, model.Location, model.Currency, model.ContactInformation), cancellationToken);
            return RedirectToAction(nameof(CheckEmail));
        }
        catch (Exception exception) when (exception is ValidationException or RequestValidationException)
        {
            AddValidationErrors(exception);
            return InvalidForm("Register", model);
        }
    }

    [AllowAnonymous, HttpGet("check-email")]
    public IActionResult CheckEmail() => View();

    [AllowAnonymous, HttpGet("confirm-email")]
    public IActionResult ConfirmEmail(Guid? userId, string? token)
    {
        if (userId.HasValue && userId.Value != Guid.Empty && !string.IsNullOrWhiteSpace(token) && token.Length < 3000)
        {
            confirmationTickets.Set(HttpContext, userId.Value, token);
            return RedirectToAction(nameof(ConfirmEmail));
        }
        return confirmationTickets.Get(HttpContext) is null ? InvalidConfirmation() : View();
    }

    [AllowAnonymous, HttpPost("confirm-email"), EnableRateLimiting("authentication")]
    public async Task<IActionResult> ConfirmEmail(CancellationToken cancellationToken)
    {
        var ticket = confirmationTickets.Get(HttpContext);
        if (ticket is null) return InvalidConfirmation();
        confirmationTickets.Clear(HttpContext);
        try
        {
            await sender.Send(new ConfirmEmailCommand(ticket.UserId, ticket.Token), cancellationToken);
            return RedirectToAction(nameof(ConfirmationSuccess));
        }
        catch (Exception exception) when (exception is ValidationException or RequestValidationException or NotFoundException)
        {
            return InvalidConfirmation();
        }
    }

    [AllowAnonymous, HttpGet("confirmation-success")]
    public IActionResult ConfirmationSuccess() => View();

    [AllowAnonymous, HttpGet("resend-confirmation")]
    public IActionResult ResendConfirmation() => View(new ResendConfirmationViewModel());

    [AllowAnonymous, HttpPost("resend-confirmation"), EnableRateLimiting("confirmation-resend")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationViewModel model, CancellationToken cancellationToken)
    {
        if (!emailOptions.Value.Enabled) return View("ResendConfirmation", model);
        if (!ModelState.IsValid) return InvalidForm("ResendConfirmation", model);
        try { await sender.Send(new ResendConfirmationCommand(model.Email), cancellationToken); }
        catch (ValidationException exception) { AddValidationErrors(exception); return InvalidForm("ResendConfirmation", model); }
        return RedirectToAction(nameof(ResendSent));
    }

    [AllowAnonymous, HttpGet("resend-sent")]
    public IActionResult ResendSent() => View();

    private ViewResult InvalidConfirmation()
    {
        var result = View("InvalidConfirmation");
        result.StatusCode = 400;
        return result;
    }

    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) => User.Identity?.IsAuthenticated == true
        ? LocalRedirect(PostAuthenticationDestination.Home)
        : View(new LoginViewModel { ReturnUrl = PostAuthenticationDestination.Resolve(false, returnUrl) });

    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("authentication")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(PostAuthenticationDestination.Home);
        if (!ModelState.IsValid) return InvalidForm("Login", model);
        try
        {
            await loginValidator.ValidateAndThrowAsync(new LoginCommand(model.Email, model.Password), cancellationToken);
            await authentication.SignInAsync(model.Email, model.Password, cancellationToken);
            return LocalRedirect(PostAuthenticationDestination.Resolve(false, model.ReturnUrl));
        }
        catch (ValidationException exception)
        {
            AddValidationErrors(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            ModelState.AddModelError(string.Empty, Text(exception.Message));
        }
        return InvalidForm("Login", model);
    }

    [Authorize(Policy = "Website"), HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await authentication.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize(Policy = "Website", Roles = "Owner,Manager"), HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken) =>
        View(await connections.IsGoogleConnectedAsync(cancellationToken));

    [AllowAnonymous, HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("~/Views/Shared/Error.cshtml", new WebsiteErrorViewModel(403,
            "Access denied", "An Owner or Manager account is required to manage products."));
    }
}
