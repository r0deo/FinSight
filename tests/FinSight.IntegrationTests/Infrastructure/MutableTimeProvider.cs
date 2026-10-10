namespace FinSight.IntegrationTests.Infrastructure;

public sealed class MutableTimeProvider : TimeProvider
{
    private readonly object _sync = new();
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_sync)
        {
            return _utcNow;
        }
    }

    public void SetUtcNow(DateTimeOffset utcNow)
    {
        lock (_sync)
        {
            _utcNow = utcNow.ToUniversalTime();
        }
    }
}
