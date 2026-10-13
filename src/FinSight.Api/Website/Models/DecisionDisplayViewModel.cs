using FinSight.Domain.Enums;
using FinSight.Domain.Models;

namespace FinSight.Api.Website.Models;

public sealed record DecisionDisplayViewModel(
    PricingDecisionResult Decision,
    SalesPeriod? Period,
    decimal CurrentPrice,
    decimal ProposedPrice,
    int CurrentQuantity,
    int? ExpectedQuantity,
    int? DeclinePercentage,
    string Currency,
    bool IsHistorical = false);
