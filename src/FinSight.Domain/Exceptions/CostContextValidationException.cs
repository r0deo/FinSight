namespace FinSight.Domain.Exceptions;
public sealed class CostContextValidationException(string field, string message) : ArgumentException(message)
{
    public string Field { get; } = field;
}
