namespace FinSight.Application.Common.Interfaces;

public interface IBusinessCurrencyReader
{
    Task<string?> GetCurrencyAsync(Guid businessId, CancellationToken cancellationToken);
}
