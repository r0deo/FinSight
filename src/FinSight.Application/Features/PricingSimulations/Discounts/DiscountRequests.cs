using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Services;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace FinSight.Application.Features.PricingSimulations.Discounts;

public interface IDiscountRequest
{
    decimal DiscountPercentage { get; }
    int CurrentSalesQuantity { get; }
    int? ExpectedSalesQuantity { get; }
    decimal? CampaignCost { get; }
    SalesPeriod ComparisonPeriod { get; }
}

public sealed record PreviewDiscountQuery(Guid ProductId, decimal DiscountPercentage, int CurrentSalesQuantity,
    int? ExpectedSalesQuantity, decimal? CampaignCost, SalesPeriod ComparisonPeriod) : IRequest<DiscountSimulationSnapshot>, IDiscountRequest;

public sealed record CreateDiscountCommand(Guid ProductId, decimal DiscountPercentage, int CurrentSalesQuantity,
    int? ExpectedSalesQuantity, decimal? CampaignCost, SalesPeriod ComparisonPeriod,
    decimal PreviewCurrentPrice, decimal PreviewUnitCost) : FinSight.Application.Features.BusinessCosts.CostLinkedRequest, IRequest<PricingSimulationResponse>, IDiscountRequest;

public sealed record UpdateDiscountCommand(Guid SimulationId, decimal DiscountPercentage, int CurrentSalesQuantity,
    int? ExpectedSalesQuantity, decimal? CampaignCost, SalesPeriod ComparisonPeriod, long Version)
    : FinSight.Application.Features.BusinessCosts.CostLinkedRequest, IRequest<PricingSimulationVersionResponse>, IDiscountRequest;

public sealed record PreviewSavedDiscountQuery(Guid SimulationId, decimal DiscountPercentage, int CurrentSalesQuantity,
    int? ExpectedSalesQuantity, decimal? CampaignCost, SalesPeriod ComparisonPeriod, long Version)
    : IRequest<DiscountSimulationSnapshot>, IDiscountRequest;

public abstract class DiscountValidator<T> : AbstractValidator<T> where T : IDiscountRequest
{
    protected DiscountValidator()
    {
        RuleFor(x => x.DiscountPercentage).Must(x => x > 0 && x < 100 && decimal.Round(x, 4) == x)
            .WithMessage("Enter a discount greater than 0 and less than 100%, with at most four decimal places.").WithErrorCode("DiscountCalculation");
        RuleFor(x => x.CurrentSalesQuantity).GreaterThan(0)
            .WithMessage("Enter a positive whole number of current sales.").WithErrorCode("DiscountCalculation");
        RuleFor(x => x.ExpectedSalesQuantity).GreaterThanOrEqualTo(0)
            .WithMessage("Expected sales must be a non-negative whole number.").WithErrorCode("DiscountCalculation");
        RuleFor(x => x.CampaignCost).Must(x => !x.HasValue || x >= 0 && x <= DiscountSimulationCalculator.MaximumMoney && decimal.Round(x.Value, 2) == x)
            .WithMessage("Enter a non-negative amount with at most two decimal places within the supported range.").WithErrorCode("DiscountCalculation");
        RuleFor(x => x.ComparisonPeriod).IsInEnum()
            .WithMessage("Choose a valid comparison period.").WithErrorCode("DiscountCalculation");
    }
}
public sealed class PreviewDiscountValidator : DiscountValidator<PreviewDiscountQuery>
{
    public PreviewDiscountValidator() => RuleFor(x => x.ProductId).NotEmpty();
}
public sealed class CreateDiscountValidator : DiscountValidator<CreateDiscountCommand>
{
    public CreateDiscountValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.PreviewCurrentPrice).InclusiveBetween(0, DiscountSimulationCalculator.MaximumMoney);
        RuleFor(x => x.PreviewUnitCost).InclusiveBetween(0, DiscountSimulationCalculator.MaximumMoney);
    }
}
public sealed class UpdateDiscountValidator : DiscountValidator<UpdateDiscountCommand>
{
    public UpdateDiscountValidator()
    {
        RuleFor(x => x.SimulationId).NotEmpty(); RuleFor(x => x.Version).GreaterThan(0);
    }
}

public sealed class PreviewSavedDiscountValidator : DiscountValidator<PreviewSavedDiscountQuery>
{
    public PreviewSavedDiscountValidator()
    {
        RuleFor(x => x.SimulationId).NotEmpty(); RuleFor(x => x.Version).GreaterThan(0);
    }
}

internal static class DiscountValidation
{
    public static T Run<T>(Func<T> operation)
    {
        try { return operation(); }
        catch (DiscountValidationException error)
        {
            throw new ValidationException([new ValidationFailure(error.Field, error.Message) { ErrorCode = "DiscountCalculation" }]);
        }
    }
}
