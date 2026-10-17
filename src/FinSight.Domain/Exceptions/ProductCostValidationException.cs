namespace FinSight.Domain.Exceptions;

public sealed class ProductCostValidationException(string message, string? fieldPath = null) : ArgumentException(message)
{
    // Input-relative path, independent of HTTP forms and localization.
    public string? FieldPath { get; } = fieldPath;
}
