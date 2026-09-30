using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Application.Features.Authentication.Commands.Login;
using FinSight.Domain.Entities;

namespace FinSight.UnitTests.Application.Features.Authentication;

public class LoginHandlerTests
{
    [Fact]
    public async Task Handle_WithCredentials_DelegatesToAuthenticationService()
    {
        // Arrange
        var expectedResult = new AuthenticationResult(
            "token",
            DateTime.UtcNow.AddHours(1),
            "refresh-token",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Manager");
        var authenticationService = new StubAuthenticationService(expectedResult);
        var handler = new LoginHandler(authenticationService);
        var command = new LoginCommand("manager@example.com", "Password1!");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Same(expectedResult, result);
        Assert.Equal("manager@example.com", authenticationService.Email);
        Assert.Equal("Password1!", authenticationService.Password);
    }

    private sealed class StubAuthenticationService(AuthenticationResult result)
        : IAuthenticationService
    {
        public string? Email { get; private set; }

        public string? Password { get; private set; }

        public Task<RegistrationResult> RegisterOwnerAsync(
            Business business,
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<RegistrationResult> CreateManagerAsync(Guid businessId, string email, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AuthenticationResult> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            Email = email;
            Password = password;

            return Task.FromResult(result);
        }

        public Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
