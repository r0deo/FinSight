namespace FinSight.Domain.Exceptions;

public sealed class PricingSimulationConflictException(string message)
    : InvalidOperationException(message);
