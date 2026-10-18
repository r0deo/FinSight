namespace FinSight.Domain.Exceptions;

public sealed class DiscountValidationException(string field, string message) : ArgumentException(message)
{
    public string Field { get; } = field;
}
