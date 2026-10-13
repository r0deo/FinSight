using FinSight.Domain.Enums;
using FinSight.Domain.Models;
using FinSight.Domain.Services;

namespace FinSight.Domain.Entities;

public sealed class WhatIfSimulation
{
    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string ProductNameSnapshot { get; private set; } = null!;
    public string CurrencySnapshot { get; private set; } = null!;
    public SalesPeriod Period { get; private set; }
    public WhatIfInputs Inputs { get; private set; } = null!;
    public WhatIfResult Result { get; private set; } = null!;
    public int CalculationVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public long Version { get; private set; }

    private WhatIfSimulation() { }

    public WhatIfSimulation(Guid id, Guid businessId, Guid productId, Guid createdByUserId,
        string productName, string currency, SalesPeriod period, WhatIfInputs inputs,
        DateTime createdAtUtc, WhatIfCalculator calculator)
    {
        if (id == Guid.Empty || businessId == Guid.Empty || productId == Guid.Empty
            || createdByUserId == Guid.Empty)
            throw new ArgumentException("IDs cannot be empty.");
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name is required.", nameof(productName));
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != Business.CurrencyLength)
            throw new ArgumentException("A three-letter currency is required.", nameof(currency));
        if (!Enum.IsDefined(period))
            throw new ArgumentOutOfRangeException(nameof(period));
        if (createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", nameof(createdAtUtc));
        ArgumentNullException.ThrowIfNull(calculator);
        var result = calculator.Calculate(inputs);
        Id = id;
        BusinessId = businessId;
        ProductId = productId;
        CreatedByUserId = createdByUserId;
        ProductNameSnapshot = productName.Trim();
        CurrencySnapshot = currency.Trim().ToUpperInvariant();
        Period = period;
        Inputs = inputs;
        Result = result;
        CalculationVersion = 1;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        Version = 1;
    }
}
