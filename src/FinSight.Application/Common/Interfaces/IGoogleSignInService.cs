using FinSight.Domain.Entities;

namespace FinSight.Application.Common.Interfaces;

public enum GoogleSignInOutcome { SignedIn, NeedsBusiness, NeedsConfirmation, NeedsLocalSignIn, Failed }
public sealed record GoogleIdentitySummary(string Email);

public interface IGoogleSignInService
{
    Task BeginAsync(string localCallbackPath, bool linkToCurrentUser = false);
    Task<GoogleSignInOutcome> CompleteSignInAsync(CancellationToken cancellationToken = default);
    Task<GoogleIdentitySummary?> GetPendingIdentityAsync();
    Task<GoogleSignInOutcome> CreateBusinessAsync(Business business, CancellationToken cancellationToken = default);
    Task<bool> LinkToCurrentUserAsync(CancellationToken cancellationToken = default);
}
