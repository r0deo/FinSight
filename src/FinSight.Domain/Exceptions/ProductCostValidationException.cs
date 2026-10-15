namespace FinSight.Domain.Exceptions;

public sealed class ProductCostValidationException(string message) : ArgumentException(message);
