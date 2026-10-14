using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FinSight.Application.Features.BusinessCosts;

public sealed record CostDetails(string Name, BusinessCostCategory Category, decimal Amount, DateOnly Month,
    CostCertainty Certainty, UnitCostInclusion Inclusion, string? Note);
public sealed record CreateBusinessCostCommand(CostDetails Details) : IRequest<Guid>;
public sealed record UpdateBusinessCostCommand(Guid Id, long Version, CostDetails Details) : IRequest;
public sealed record DeleteBusinessCostCommand(Guid Id, long Version) : IRequest;
public sealed record GetBusinessCostQuery(Guid Id) : IRequest<BusinessCostResponse>;
public sealed record GetBusinessCostsQuery(DateOnly Month) : IRequest<BusinessCostSummary>;
public sealed record BusinessCostResponse(Guid Id, string Name, BusinessCostCategory Category, decimal Amount,
    DateOnly Month, CostCertainty Certainty, UnitCostInclusion Inclusion, string? Note, long Version)
{
    public static BusinessCostResponse From(BusinessCost c) => new(c.Id, c.Name, c.Category, c.Amount,
        c.Month, c.Certainty, c.Inclusion, c.Note, c.Version);
}
public sealed record CategoryTotal(BusinessCostCategory Category, decimal Actual, decimal Estimated);
public sealed record BusinessCostSummary(DateOnly Month, IReadOnlyList<BusinessCostResponse> Entries)
{
    public long ReviewVersion { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public bool IsReviewed => ReviewedAtUtc.HasValue;
    public bool HasEstimates => Entries.Any(c => c.Certainty == CostCertainty.Estimated);
    public decimal OperatingActual => OperatingCategories.Sum(c => c.Actual);
    public decimal OperatingEstimated => OperatingCategories.Sum(c => c.Estimated);
    public decimal RecordedOperatingTotal => OperatingActual + OperatingEstimated;
    public IReadOnlyList<CategoryTotal> OperatingCategories => Entries
        .Where(c => c.Category != BusinessCostCategory.Inventory).GroupBy(c => c.Category)
        .Select(g => new CategoryTotal(g.Key, g.Where(c => c.Certainty == CostCertainty.Actual).Sum(c => c.Amount),
            g.Where(c => c.Certainty == CostCertainty.Estimated).Sum(c => c.Amount)))
        .OrderByDescending(c => c.Actual + c.Estimated).ThenBy(c => c.Category).ToArray();
    public decimal InventoryActual => Entries.Where(c => c.Category == BusinessCostCategory.Inventory && c.Certainty == CostCertainty.Actual).Sum(c => c.Amount);
    public decimal InventoryEstimated => Entries.Where(c => c.Category == BusinessCostCategory.Inventory && c.Certainty == CostCertainty.Estimated).Sum(c => c.Amount);
    public int UnresolvedCount => Entries.Count(c => c.Inclusion == UnitCostInclusion.Unsure);
    // Deliberately no profit, expense grand total, or allocation/deduction from product contribution.
}
public sealed class CostDetailsValidator : AbstractValidator<CostDetails>
{
    public CostDetailsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length <= 100);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Amount).InclusiveBetween(0, BusinessCost.MaximumAmount).Must(x => decimal.Round(x, 2) == x);
        RuleFor(x => x.Month).Must(ValidMonth);
        RuleFor(x => x.Certainty).IsInEnum();
        RuleFor(x => x.Inclusion).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
    }
    public static bool ValidMonth(DateOnly month) => month.Day == 1 && month.Year is >= 2000 and <= 2100;
}
public sealed class CreateBusinessCostValidator : AbstractValidator<CreateBusinessCostCommand>
{
    public CreateBusinessCostValidator() => RuleFor(x => x.Details).NotNull().SetValidator(new CostDetailsValidator());
}
public sealed class UpdateBusinessCostValidator : AbstractValidator<UpdateBusinessCostCommand>
{
    public UpdateBusinessCostValidator()
    {
        RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Details).NotNull().SetValidator(new CostDetailsValidator());
    }
}
public sealed class DeleteBusinessCostValidator : AbstractValidator<DeleteBusinessCostCommand>
{
    public DeleteBusinessCostValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Version).GreaterThan(0); }
}
public sealed class GetBusinessCostValidator : AbstractValidator<GetBusinessCostQuery>
{
    public GetBusinessCostValidator() => RuleFor(x => x.Id).NotEmpty();
}
public sealed class GetBusinessCostsValidator : AbstractValidator<GetBusinessCostsQuery>
{
    public GetBusinessCostsValidator() => RuleFor(x => x.Month).Must(CostDetailsValidator.ValidMonth);
}
public sealed class BusinessCostHandlers(IBusinessCostRepository costs, ICurrentUserContext user) :
    IRequestHandler<CreateBusinessCostCommand, Guid>, IRequestHandler<UpdateBusinessCostCommand>,
    IRequestHandler<DeleteBusinessCostCommand>, IRequestHandler<GetBusinessCostQuery, BusinessCostResponse>,
    IRequestHandler<GetBusinessCostsQuery, BusinessCostSummary>
{
    public async Task<Guid> Handle(CreateBusinessCostCommand request, CancellationToken ct)
    {
        var d = request.Details;
        var cost = new BusinessCost(Guid.NewGuid(), user.BusinessId, d.Name, d.Category, d.Amount, d.Month, d.Certainty, d.Inclusion, d.Note);
        await costs.AddAsync(cost, ct); await costs.SaveChangesAsync(ct); return cost.Id;
    }
    public async Task Handle(UpdateBusinessCostCommand request, CancellationToken ct)
    {
        var cost = await Required(request.Id, true, ct);
        if (cost.Version != request.Version) throw new ConcurrencyConflictException();
        var d = request.Details;
        cost.Update(d.Name, d.Category, d.Amount, d.Month, d.Certainty, d.Inclusion, d.Note);
        await costs.SaveChangesAsync(ct);
    }
    public async Task Handle(DeleteBusinessCostCommand request, CancellationToken ct)
    {
        var cost = await Required(request.Id, true, ct);
        if (cost.Version != request.Version) throw new ConcurrencyConflictException();
        costs.Remove(cost); await costs.SaveChangesAsync(ct);
    }
    public async Task<BusinessCostResponse> Handle(GetBusinessCostQuery request, CancellationToken ct) =>
        BusinessCostResponse.From(await Required(request.Id, false, ct));
    public async Task<BusinessCostSummary> Handle(GetBusinessCostsQuery request, CancellationToken ct)
    {
        var snapshot = await costs.GetMonthSnapshotAsync(user.BusinessId, request.Month, ct);
        return new(request.Month, snapshot.Entries.Select(BusinessCostResponse.From).ToArray())
        { ReviewVersion = snapshot.Review?.Version ?? 0, ReviewedAtUtc = snapshot.Review?.ReviewedAtUtc };
    }
    private async Task<BusinessCost> Required(Guid id, bool tracked, CancellationToken ct) =>
        await costs.GetAsync(id, user.BusinessId, tracked, ct) ?? throw new NotFoundException(nameof(BusinessCost), id);
}
