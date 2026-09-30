namespace FinSight.Api.Contracts.PricingSimulations;

public sealed record PreviewPricingSimulationRequest(
    Guid ProductId,
    decimal ProposedPrice,
    int CurrentSalesQuantity,
    int ExpectedSalesQuantity);
