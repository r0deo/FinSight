using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Models;

namespace FinSight.Application.Features.WhatIf;

public sealed record WhatIfResponse(Guid Id, Guid ProductId, string ProductName,
    string Currency, SalesPeriod Period, WhatIfInputs Inputs, WhatIfResult Result,
    DateTime CreatedAtUtc, long Version)
{
    public static WhatIfResponse From(WhatIfSimulation value) => new(value.Id,
        value.ProductId, value.ProductNameSnapshot, value.CurrencySnapshot,
        value.Period, value.Inputs, value.Result, value.CreatedAtUtc, value.Version);
}
