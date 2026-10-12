using FinSight.Api.Website.Models;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Authentication.Commands.GoogleOnboarding;
using FinSight.Infrastructure.Identity;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace FinSight.Api.Website.Controllers;

[Route("account/google")]
public sealed class GoogleAccountController(IGoogleSignInService google, ISender sender,
    IOptions<GoogleSignInOptions> options) : WebsiteController
{
    [AllowAnonymous, HttpPost("start"), EnableRateLimiting("authentication")]
    public async Task<IActionResult> Start(string? returnUrl)
    {
        if (!options.Value.Enabled) return View("Unavailable");
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(PostAuthenticationDestination.Home);
        var target = PostAuthenticationDestination.Resolve(false, returnUrl);
        await google.BeginAsync("/account/google/callback?returnUrl=" + Uri.EscapeDataString(target));
        return new EmptyResult();
    }

    [AllowAnonymous, HttpGet("callback")]
    public async Task<IActionResult> Callback(string? returnUrl, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(PostAuthenticationDestination.Home);
        var result = await google.CompleteSignInAsync(cancellationToken);
        if (result == GoogleSignInOutcome.SignedIn)
            return LocalRedirect(PostAuthenticationDestination.Resolve(false, returnUrl));
        return Outcome(result);
    }

    [AllowAnonymous, HttpGet("business")]
    public async Task<IActionResult> Business()
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(PostAuthenticationDestination.Home);
        var identity = await google.GetPendingIdentityAsync();
        if (identity is null) return RedirectToAction(nameof(Error));
        ViewData["GoogleEmail"] = identity.Email;
        return View(new GoogleBusinessViewModel());
    }

    [AllowAnonymous, HttpPost("business"), EnableRateLimiting("authentication")]
    public async Task<IActionResult> Business(GoogleBusinessViewModel model, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(PostAuthenticationDestination.Home);
        var identity = await google.GetPendingIdentityAsync();
        if (identity is null) return RedirectToAction(nameof(Error));
        ViewData["GoogleEmail"] = identity.Email;
        if (!ModelState.IsValid) return InvalidForm("Business", model);
        try
        {
            var result = await sender.Send(new GoogleOnboardingCommand(model.BusinessName, model.Location,
                model.Currency, model.ContactInformation), cancellationToken);
            if (result == GoogleSignInOutcome.SignedIn) return LocalRedirect(PostAuthenticationDestination.Resolve(false));
            return Outcome(result);
        }
        catch (Exception exception) when (exception is ValidationException or RequestValidationException)
        {
            AddValidationErrors(exception);
            return InvalidForm("Business", model);
        }
    }

    [Authorize(Policy = "Website", Roles = "Owner,Manager"), HttpGet("link")]
    // Old bookmarks remain useful, but the connection action lives only in settings.
    public IActionResult Link() => View();

    [Authorize(Policy = "Website", Roles = "Owner,Manager"), HttpPost("link"), EnableRateLimiting("authentication")]
    public async Task<IActionResult> StartLink()
    {
        if (!options.Value.Enabled) return View("Unavailable");
        await google.BeginAsync("/account/google/link-callback", linkToCurrentUser: true);
        return new EmptyResult();
    }

    [Authorize(Policy = "Website", Roles = "Owner,Manager"), HttpGet("link-callback")]
    public async Task<IActionResult> LinkCallback(CancellationToken cancellationToken)
    {
        if (!await google.LinkToCurrentUserAsync(cancellationToken)) return RedirectToAction(nameof(Error));
        TempData["Success"] = "Google sign-in linked to your existing account. Your business and role have not changed.";
        return RedirectToAction("Settings", "Account");
    }

    [AllowAnonymous, HttpGet("error")]
    public IActionResult Error() => View();

    [AllowAnonymous, HttpGet("existing-account")]
    public IActionResult ExistingAccount() => View();

    private IActionResult Outcome(GoogleSignInOutcome outcome) => outcome switch
    {
        GoogleSignInOutcome.NeedsBusiness => LocalRedirect(PostAuthenticationDestination.Resolve(true)),
        GoogleSignInOutcome.NeedsConfirmation => RedirectToAction("CheckEmail", "Account"),
        GoogleSignInOutcome.NeedsLocalSignIn => RedirectToAction(nameof(ExistingAccount)),
        _ => RedirectToAction(nameof(Error))
    };
}
