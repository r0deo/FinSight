using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FinSight.Application.Features.BusinessCosts;

public sealed record ReviewBusinessCostMonthCommand(DateOnly Month, long Version, bool Reviewed) : IRequest;
public sealed class ReviewBusinessCostMonthValidator : AbstractValidator<ReviewBusinessCostMonthCommand>
{
    public ReviewBusinessCostMonthValidator()
    {
        RuleFor(x => x.Month).Must(CostDetailsValidator.ValidMonth);
        RuleFor(x => x.Version).GreaterThanOrEqualTo(0);
    }
}
public sealed class ReviewBusinessCostMonthHandler(IBusinessCostRepository costs, ICurrentUserContext user, TimeProvider clock)
    : IRequestHandler<ReviewBusinessCostMonthCommand>
{
    public async Task Handle(ReviewBusinessCostMonthCommand request, CancellationToken ct)
    {
        if (user.Role != "Owner") throw new ForbiddenAccessException("Only owners can review a month.");
        var month = await costs.GetTrackedMonthAsync(user.BusinessId, request.Month, ct);
        if ((month?.Version ?? 0) != request.Version) throw new ConcurrencyConflictException();
        if (request.Reviewed && (await costs.GetMonthAsync(user.BusinessId, request.Month, ct)).Count == 0)
            throw new ValidationException("Record at least one cost before reviewing a month.");
        if (month is null)
        {
            month = new BusinessCostMonth(user.BusinessId, request.Month);
            await costs.AddMonthAsync(month, ct);
        }
        if (request.Reviewed) month.Review(user.UserId, clock.GetUtcNow().UtcDateTime);
        else month.Reopen();
        await costs.SaveChangesAsync(ct);
    }
}

// Comparison of recorded figures only. An absent month is unknown, not zero.
public sealed record CostMonthComparison(BusinessCostSummary Current, BusinessCostSummary? Previous)
{
    public bool CanCompare => Current.Entries.Count > 0 && Previous?.Entries.Count > 0;
    public bool NeedsCaution => !Current.IsReviewed || Previous?.IsReviewed != true || Current.HasEstimates || Previous.HasEstimates;
    public decimal? OperatingChange => CanCompare ? Current.RecordedOperatingTotal - Previous!.RecordedOperatingTotal : null;
    public decimal? OperatingPercent => CanCompare && Previous!.RecordedOperatingTotal > 0
        ? OperatingChange / Previous.RecordedOperatingTotal * 100 : null;
    public decimal? InventoryChange => CanCompare ? Current.InventoryActual + Current.InventoryEstimated
        - Previous!.InventoryActual - Previous.InventoryEstimated : null;
}

public sealed record CostAdjustmentResult(BusinessCostCategory Category, decimal Percent, decimal CategoryAmount,
    decimal PotentialSaving, decimal RevisedRecordedTotal, BusinessCostSummary Source);
public sealed record PreviewCostAdjustmentQuery(DateOnly Month, BusinessCostCategory Category, decimal Percent) : IRequest<CostAdjustmentResult>;
public sealed class PreviewCostAdjustmentValidator : AbstractValidator<PreviewCostAdjustmentQuery>
{
    public PreviewCostAdjustmentValidator()
    {
        RuleFor(x => x.Month).Must(CostDetailsValidator.ValidMonth);
        RuleFor(x => x.Category).IsInEnum().NotEqual(BusinessCostCategory.Inventory);
        RuleFor(x => x.Percent).InclusiveBetween(0, 100).Must(x => decimal.Round(x, 2) == x);
    }
}
public sealed class PreviewCostAdjustmentHandler(ISender sender) : IRequestHandler<PreviewCostAdjustmentQuery, CostAdjustmentResult>
{
    public async Task<CostAdjustmentResult> Handle(PreviewCostAdjustmentQuery request, CancellationToken ct) =>
        Calculate(await sender.Send(new GetBusinessCostsQuery(request.Month), ct), request.Category, request.Percent);

    public static CostAdjustmentResult Calculate(BusinessCostSummary summary, BusinessCostCategory category, decimal percent)
    {
        if (!Enum.IsDefined(category) || category == BusinessCostCategory.Inventory) throw new ArgumentOutOfRangeException(nameof(category));
        if (percent is < 0 or > 100 || decimal.Round(percent, 2) != percent) throw new ArgumentOutOfRangeException(nameof(percent));
        var total = summary.OperatingCategories.SingleOrDefault(c => c.Category == category)
            ?? throw new ValidationException("Choose a recorded operating-cost category.");
        var amount = total.Actual + total.Estimated;
        var saving = decimal.Round(amount * percent / 100, 2, MidpointRounding.AwayFromZero);
        return new(category, percent, amount, saving, summary.RecordedOperatingTotal - saving, summary);
    }
}
