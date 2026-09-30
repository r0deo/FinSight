using FinSight.Application.Common.Models;
using FinSight.Domain.Entities;

namespace FinSight.Application.Common.Interfaces;

public interface IAuthenticationService
{
    Task<RegistrationResult> RegisterOwnerAsync(
        Business business,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<RegistrationResult> CreateManagerAsync(
        Guid businessId,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);

    Task RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        Guid userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
}
