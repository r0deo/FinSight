using FinSight.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class BusinessCurrencyReader(FinSightDbContext dbContext) : IBusinessCurrencyReader
{
    public Task<string?> GetCurrencyAsync(Guid businessId, CancellationToken cancellationToken) =>
        dbContext.Businesses.AsNoTracking().Where(business => business.Id == businessId)
            .Select(business => business.Currency).SingleOrDefaultAsync(cancellationToken);
}
