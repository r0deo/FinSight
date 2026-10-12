using FinSight.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace FinSight.Infrastructure.Identity;

public sealed class AccountConnections(UserManager<ApplicationUser> users, ICurrentUserContext currentUser)
    : IAccountConnections
{
    public async Task<bool> IsGoogleConnectedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(currentUser.UserId.ToString());
        if (user is null || user.BusinessId != currentUser.BusinessId)
            throw new UnauthorizedAccessException("Your session is no longer valid. Please sign in again.");

        return (await users.GetLoginsAsync(user)).Any(login => login.LoginProvider == "Google");
    }
}
