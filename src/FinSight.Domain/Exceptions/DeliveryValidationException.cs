namespace FinSight.Domain.Exceptions;

public sealed class DeliveryValidationException(string field, string message) : ArgumentException(message)
{
    public string Field { get; } = field;
}
