namespace FinSight.Api.Contracts.PricingSimulations;

public sealed record UpdatePricingSimulationRequest(
    decimal ProposedPrice,
    int CurrentSalesQuantity,
    int ExpectedSalesQuantity);
