using FinSight.Api.Website.Models;
using FinSight.Application.Common.Exceptions;
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
                NotFoundException => new WebsiteErrorViewModel(404, "We couldn't find that product", "It may be unavailable. Return to your products to continue."),
                ForbiddenAccessException => new WebsiteErrorViewModel(403, "Access denied", "Only owners and managers can manage products."),
                ValidationException or RequestValidationException => new WebsiteErrorViewModel(400, "Check your request", "The supplied values are invalid. Page must be at least 1 and page size must be between 1 and 100."),
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
