namespace FinSight.Application.Common.Interfaces;

// Connection state belongs to the account, not to the current sign-in method.
public interface IAccountConnections
{
    Task<bool> IsGoogleConnectedAsync(CancellationToken cancellationToken = default);
}
