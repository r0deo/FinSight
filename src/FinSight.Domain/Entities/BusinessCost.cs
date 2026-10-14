using FinSight.Domain.Enums;
namespace FinSight.Domain.Entities;

public sealed class BusinessCost
{
    public const decimal MaximumAmount = 999_999_999_999.99m;
    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public string Name { get; private set; } = "";
    public BusinessCostCategory Category { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly Month { get; private set; }
    public CostCertainty Certainty { get; private set; }
    public UnitCostInclusion Inclusion { get; private set; }
    public string? Note { get; private set; }
    public long Version { get; private set; } = 1;
    private BusinessCost() { }
    public BusinessCost(Guid id, Guid businessId, string name, BusinessCostCategory category,
        decimal amount, DateOnly month, CostCertainty certainty, UnitCostInclusion inclusion, string? note)
    {
        if (id == Guid.Empty || businessId == Guid.Empty) throw new ArgumentException("IDs are required.");
        Id = id; BusinessId = businessId;
        SetDetails(name, category, amount, month, certainty, inclusion, note);
    }
    public void Update(string name, BusinessCostCategory category, decimal amount, DateOnly month,
        CostCertainty certainty, UnitCostInclusion inclusion, string? note)
    {
        SetDetails(name, category, amount, month, certainty, inclusion, note);
        Version = checked(Version + 1);
    }
    private void SetDetails(string name, BusinessCostCategory category, decimal amount, DateOnly month,
        CostCertainty certainty, UnitCostInclusion inclusion, string? note)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new ArgumentException("Invalid cost name.");
        if (!Enum.IsDefined(category) || !Enum.IsDefined(certainty) || !Enum.IsDefined(inclusion))
            throw new ArgumentException("Invalid cost classification.");
        if (amount < 0 || amount > MaximumAmount || decimal.Round(amount, 2) != amount)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (month.Day != 1 || month.Year < 2000 || month.Year > 2100)
            throw new ArgumentOutOfRangeException(nameof(month));
        if (note?.Length > 500) throw new ArgumentException("Note is too long.");
        Name = name.Trim(); Category = category; Amount = amount; Month = month;
        Certainty = certainty; Inclusion = inclusion; Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
