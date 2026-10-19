using FinSight.Application.Common.Exceptions;
using FinSight.Api.Website.Localization;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

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
                ModelState.AddModelError(error.PropertyName, LocalizedValidation(error));
        }
        else if (exception is RequestValidationException request)
        {
            foreach (var (code, messages) in request.Errors)
                foreach (var message in messages)
                    ModelState.AddModelError(string.Empty, code switch
                    {
                        "DuplicateEmail" => Text("An account with this email already exists."),
                        "PasswordTooShort" => Text("The password is too short."),
                        "PasswordRequiresNonAlphanumeric" => Text("The password needs a symbol."),
                        "PasswordRequiresDigit" => Text("The password needs a number."),
                        "PasswordRequiresLower" => Text("The password needs a lowercase letter."),
                        "PasswordRequiresUpper" => Text("The password needs an uppercase letter."),
                        "InvalidEmail" => Text("Enter a valid email address."),
                        _ => Text(message) == message
                            ? Text("Please check the highlighted fields and try again.")
                            : Text(message)
                    });
        }
    }

    protected string Text(string key, params object[] args)
    {
        // Directly constructed controllers in unit tests have no service provider.
        var localizer = HttpContext?.RequestServices?.GetService<IStringLocalizer<RebazText>>();
        return localizer is null ? (args.Length == 0 ? key : string.Format(key, args))
            : localizer[key, args].Value;
    }

    private string LocalizedValidation(ValidationFailure error)
    {
        var propertyName = error.PropertyName.StartsWith("Request.", StringComparison.Ordinal)
            ? error.PropertyName["Request.".Length..] : error.PropertyName;
        var label = Text(propertyName switch
        {
            "BusinessName" => "Business name", "ContactInformation" => "Business contact information",
            "SellingPrice" => "Selling price", "UnitCost" => "Unit cost",
            "ProposedPrice" => "Proposed price", "CurrentSalesQuantity" => "Current sales quantity",
            "ExpectedSalesQuantity" => "Expected sales quantity", "Name" => "Product name",
            "DeclinePercentage" => "Illustrative sales decline (%)",
            "DiscountPercentage" => "Discount percentage (%)", "CampaignCost" => "Additional campaign cost (optional)",
            "ComparisonPeriod" => "Comparison period",
            "CurrentQuantity" => "Current units sold", "SalesDeclinePercent" => "Sales decline (%)",
            "CostIncreasePercent" => "Cost increase (%)", "NewUnitCost" => "New total unit cost",
            "AlternativePrice" => "Alternative selling price",
            "AlternativePriceQuantity" => "Assumed units sold at this price",
            "AlternativeSupplierCost" => "Alternative total unit cost",
            "AlternativeSupplierQuantity" => "Assumed units sold with this supplier",
            _ => propertyName
        });
        var values = error.FormattedMessagePlaceholderValues;
        object Get(string name) => values is not null && values.TryGetValue(name, out var value) ? value : "";
        return error.ErrorCode switch
        {
            "DiscountCalculation" or "DeliveryCalculation" => Text(error.ErrorMessage),
            "NotEmptyValidator" => Text("{0} must not be empty.", label),
            "EmailValidator" => Text("{0} is not a valid email address.", label),
            "MaximumLengthValidator" => Text("{0} must not be more than {1} characters.", label, Get("MaxLength")),
            "MinimumLengthValidator" => Text("{0} must be at least {1} characters.", label, Get("MinLength")),
            "ExactLengthValidator" => Text("{0} must be {1} characters in length.", label, Get("MaxLength")),
            "LengthValidator" when Equals(Get("MinLength"), Get("MaxLength")) =>
                Text("{0} must be {1} characters in length.", label, Get("MaxLength")),
            "GreaterThanOrEqualValidator" => Text("{0} must be greater than or equal to {1}.", label, Get("ComparisonValue")),
            "InclusiveBetweenValidator" => Text("{0} must be between {1} and {2}.", label, Get("From"), Get("To")),
            _ => Text("Please check the highlighted fields and try again.")
        };
    }

    protected ViewResult InvalidForm(string viewName, object model)
    {
        var result = View(viewName, model);
        result.StatusCode = StatusCodes.Status400BadRequest;
        return result;
    }
}
