namespace FinSight.Domain.Entities;

public sealed class Business
{
    public const int MaxNameLength = 100;
    public const int MaxLocationLength = 200;
    public const int CurrencyLength = 3;
    public const int MaxContactInformationLength = 200;

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Location { get; private set; } = null!;

    public string Currency { get; private set; } = null!;

    public string ContactInformation { get; private set; } = null!;

    private Business() { }

    public Business(
        Guid id,
        string name,
        string location,
        string currency,
        string contactInformation)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Business ID cannot be empty.", nameof(id));
        }

        Id = id;
        Name = ValidateRequired(name, MaxNameLength, nameof(name));
        Location = ValidateRequired(location, MaxLocationLength, nameof(location));
        ContactInformation = ValidateRequired(
            contactInformation,
            MaxContactInformationLength,
            nameof(contactInformation));

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();

        if (normalizedCurrency.Length != CurrencyLength)
        {
            throw new ArgumentException(
                $"Currency must be a {CurrencyLength}-letter code.",
                nameof(currency));
        }

        Currency = normalizedCurrency;
    }

    private static string ValidateRequired(
        string value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        var trimmedValue = value.Trim();

        if (trimmedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return trimmedValue;
    }
}
