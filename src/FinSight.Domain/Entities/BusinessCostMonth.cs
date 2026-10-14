namespace FinSight.Domain.Entities;

// The owner's declaration about recorded entries, not verified accounting completeness.
public sealed class BusinessCostMonth
{
    private BusinessCostMonth() { }
    public BusinessCostMonth(Guid businessId, DateOnly month)
    {
        if (businessId == Guid.Empty) throw new ArgumentException("A business is required.", nameof(businessId));
        if (month.Day != 1 || month.Year is < 2000 or > 2100) throw new ArgumentOutOfRangeException(nameof(month));
        BusinessId = businessId; Month = month;
    }
    public Guid BusinessId { get; private set; }
    public DateOnly Month { get; private set; }
    public long Version { get; private set; } = 1;
    public DateTime? ReviewedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public void Review(Guid userId, DateTime utcNow)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A reviewer is required.", nameof(userId));
        if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(utcNow));
        ReviewedAtUtc = utcNow; ReviewedByUserId = userId; Version = checked(Version + 1);
    }
    public void Reopen()
    {
        ReviewedAtUtc = null; ReviewedByUserId = null; Version = checked(Version + 1);
    }
}
