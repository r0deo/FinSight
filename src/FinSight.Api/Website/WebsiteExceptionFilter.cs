using FinSight.Api.Website.Models;
using FinSight.Application.Common.Exceptions;
using FinSight.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace FinSight.Api.Website;

public sealed class WebsiteExceptionFilter(
    IModelMetadataProvider metadataProvider,
    ILogger<WebsiteExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is UnauthorizedAccessException)
        {
            context.Result = new ChallengeResult(IdentityConstants.ApplicationScheme);
        }
        else
        {
            var model = context.Exception switch
            {
                NotFoundException => new WebsiteErrorViewModel(404, "We couldn't find that record", "It may be unavailable or belong to another business."),
                ForbiddenAccessException => new WebsiteErrorViewModel(403, "Access denied", "Only owners and managers can manage products."),
                ValidationException or RequestValidationException or ArgumentOutOfRangeException =>
                    new WebsiteErrorViewModel(400, "Check your request", "Please check the supplied values and try again."),
                PricingSimulationConflictException or ConcurrencyConflictException =>
                    new WebsiteErrorViewModel(409, "This simulation changed", "Recalculate the preview, then save it again."),
                _ => new WebsiteErrorViewModel(500, "Something went wrong", "We couldn't complete your request. Please try again shortly.")
            };
            if (model.StatusCode == 500)
            {
                logger.LogError(context.Exception, "A website request failed.");
            }
            context.Result = new ViewResult
            {
                ViewName = "~/Views/Shared/Error.cshtml",
                StatusCode = model.StatusCode,
                ViewData = new ViewDataDictionary<WebsiteErrorViewModel>(metadataProvider, context.ModelState) { Model = model }
            };
        }
        context.ExceptionHandled = true;
    }
}
