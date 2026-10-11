using FinSight.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Website.Controllers;

[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ServiceFilter(typeof(WebsiteExceptionFilter))]
[ApiExplorerSettings(IgnoreApi = true)]
public abstract class WebsiteController : Controller
{
    protected void AddValidationErrors(Exception exception)
    {
        if (exception is ValidationException validation)
        {
            foreach (var error in validation.Errors)
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }
        else if (exception is RequestValidationException request)
        {
            foreach (var message in request.Errors.Values.SelectMany(messages => messages))
                ModelState.AddModelError(string.Empty, message);
        }
    }

    protected ViewResult InvalidForm(string viewName, object model)
    {
        var result = View(viewName, model);
        result.StatusCode = StatusCodes.Status400BadRequest;
        return result;
    }
}
