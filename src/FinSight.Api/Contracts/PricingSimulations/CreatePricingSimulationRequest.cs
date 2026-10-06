namespace FinSight.Api.Contracts.PricingSimulations;

public sealed record CreatePricingSimulationRequest(
    Guid ProductId,
    decimal ProposedPrice,
    int CurrentSalesQuantity,
    int ExpectedSalesQuantity);
