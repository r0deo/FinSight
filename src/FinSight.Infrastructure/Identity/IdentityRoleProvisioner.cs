using FinSight.Application.Common.Authorization;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Identity;

internal static class IdentityRoleProvisioner
{
    public static async Task EnsureAsync(FinSightDbContext database, RoleManager<IdentityRole<Guid>> roles,
        CancellationToken cancellationToken)
    {
        // Shared by local and Google registration; first registrations may run concurrently.
        var ownTransaction = database.Database.CurrentTransaction is null;
        await using var transaction = ownTransaction ? await database.Database.BeginTransactionAsync(cancellationToken) : null;
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(624108324917)", cancellationToken);
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(new IdentityRole<Guid>(role));
                if (!result.Succeeded) throw new InvalidOperationException("Unable to provision application roles.");
            }
        }
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }
}
