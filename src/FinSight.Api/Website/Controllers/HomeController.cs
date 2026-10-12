using FinSight.Api.Website.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using FinSight.Application.Features.Products.Queries.GetProducts;

namespace FinSight.Api.Website.Controllers;

public sealed class HomeController(ISender sender) : WebsiteController
{
    [AllowAnonymous, HttpGet("/")]
    public IActionResult Index() => User.Identity?.IsAuthenticated == true
        ? LocalRedirect(PostAuthenticationDestination.Home) : View();

    [Authorize(Policy = "Website", Roles = "Owner,Manager"), HttpGet("/home")]
    public async Task<IActionResult> Welcome(CancellationToken cancellationToken) =>
        View(await sender.Send(new GetProductsQuery(PageSize: 1, IncludeInactive: true), cancellationToken));

    // Status-code re-execution preserves the original HTTP verb. This action only renders an error.
    [AllowAnonymous, Route("/error/{statusCode:int}"), IgnoreAntiforgeryToken]
    public IActionResult Error(int statusCode)
    {
        var code = statusCode is 400 or 401 or 403 or 404 or 405 or 429 ? statusCode : 500;
        Response.StatusCode = code;
        var message = code switch
        {
            400 => "Your form or request could not be accepted. Reload the page and try again.",
            404 => "That page could not be found. Use the links below to continue.",
            429 => "Too many attempts. Please wait a minute before trying again.",
            _ => "We couldn't complete this request. Return to your products or try again."
        };
        return View("~/Views/Shared/Error.cshtml", new WebsiteErrorViewModel(code, "Let's get you back on track", message));
    }
}
