namespace FinSight.Domain.Entities;

public class Product
{
    public const int MaxNameLength = 60;

    public Guid Id { get; private set; }

    public Guid BusinessId { get; private set; }

    public string Name { get; private set; } = null!;

    public string Category { get; private set; } = null!;

    public string? Size { get; private set; }

    public decimal SellingPrice { get; private set; }

    public decimal UnitCost { get; private set; }

    public bool IsActive { get; private set; }

    private Product() { }

    public Product(
        Guid id,
        Guid businessId,
        string name,
        string category,
        string? size,
        decimal sellingPrice,
        decimal unitCost)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(id));
        }

        if (businessId == Guid.Empty)
        {
            throw new ArgumentException("Business ID cannot be empty.", nameof(businessId));
        }

        Id = id;
        BusinessId = businessId;
        UpdateDetails(name, category, size);
        UpdateFinancials(sellingPrice, unitCost);
        IsActive = true;
    }

    public void UpdateDetails(string name, string category, string? size)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name cannot be empty.", nameof(name));
        }

        var trimmedName = name.Trim();

        if (trimmedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Product name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Product category cannot be empty.", nameof(category));
        }

        Name = trimmedName;
        Category = category.Trim();
        Size = size?.Trim();
    }

    public void UpdateFinancials(decimal sellingPrice, decimal unitCost)
    {
        if (sellingPrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sellingPrice),
                "Selling price cannot be negative.");
        }

        if (unitCost < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitCost),
                "Unit cost cannot be negative.");
        }

        SellingPrice = sellingPrice;
        UnitCost = unitCost;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
