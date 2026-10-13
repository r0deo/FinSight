using FinSight.Domain.Enums;
using FluentValidation;
using FinSight.Domain.Services;

namespace FinSight.Application.Features.WhatIf;

public sealed record WhatIfRequest(Guid ProductId, int CurrentQuantity, SalesPeriod Period,
    CostShockInputMode CostInputMode, decimal? CostIncreasePercent, decimal? NewUnitCost,
    decimal SalesDeclinePercent, decimal? AlternativePrice, int? AlternativePriceQuantity,
    decimal? AlternativeSupplierCost, int? AlternativeSupplierQuantity);

public sealed class WhatIfRequestValidator : AbstractValidator<WhatIfRequest>
{
    public WhatIfRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Period).IsInEnum();
        RuleFor(x => x.CostInputMode).IsInEnum();
        RuleFor(x => x.CurrentQuantity).InclusiveBetween(0, WhatIfCalculator.MaxQuantity);
        RuleFor(x => x.SalesDeclinePercent).InclusiveBetween(0, 100)
            .Must(x => decimal.Round(x, 2) == x);
        RuleFor(x => x.CostIncreasePercent).NotNull()
            .When(x => x.CostInputMode == CostShockInputMode.Percentage);
        RuleFor(x => x.CostIncreasePercent).Null()
            .When(x => x.CostInputMode == CostShockInputMode.NewUnitCost);
        RuleFor(x => x.CostIncreasePercent).InclusiveBetween(0, WhatIfCalculator.MaxCostIncreasePercent)
            .Must(x => !x.HasValue || decimal.Round(x.Value, 2) == x.Value)
            .When(x => x.CostIncreasePercent.HasValue);
        RuleFor(x => x.NewUnitCost).NotNull()
            .When(x => x.CostInputMode == CostShockInputMode.NewUnitCost);
        RuleFor(x => x.NewUnitCost).Null()
            .When(x => x.CostInputMode == CostShockInputMode.Percentage);
        RuleFor(x => x.NewUnitCost).Must(ValidMoney).When(x => x.NewUnitCost.HasValue);
        RuleFor(x => x.AlternativePrice).Must(ValidMoney)
            .When(x => x.AlternativePrice.HasValue);
        RuleFor(x => x.AlternativeSupplierCost).Must(ValidMoney)
            .When(x => x.AlternativeSupplierCost.HasValue);
        RuleFor(x => x.AlternativePriceQuantity).InclusiveBetween(0, WhatIfCalculator.MaxQuantity)
            .When(x => x.AlternativePriceQuantity.HasValue);
        RuleFor(x => x.AlternativeSupplierQuantity).InclusiveBetween(0, WhatIfCalculator.MaxQuantity)
            .When(x => x.AlternativeSupplierQuantity.HasValue);
        RuleFor(x => x.AlternativePriceQuantity).Null().When(x => !x.AlternativePrice.HasValue);
        RuleFor(x => x.AlternativeSupplierQuantity).Null().When(x => !x.AlternativeSupplierCost.HasValue);
    }

    private static bool ValidMoney(decimal? value) => !value.HasValue ||
        value.Value >= 0 && value.Value <= WhatIfCalculator.MaxUnitAmount
        && decimal.Round(value.Value, 2) == value.Value;
}
