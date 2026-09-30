using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Application.Features.Authentication.Commands.Register;
using FinSight.Domain.Entities;

namespace FinSight.UnitTests.Application.Features.Authentication;

public class RegisterBusinessHandlerTests
{
    [Fact]
    public async Task Handle_WithValidCommand_RegistersFirstOwner()
    {
        // Arrange
        var expectedResult = CreateResult();
        var authenticationService = new StubAuthenticationService(expectedResult);
        var handler = new RegisterBusinessHandler(authenticationService);
        var command = new RegisterBusinessCommand(
            "owner@example.com",
            "Password1!",
            "FinSight Coffee",
            "Baghdad",
            "USD",
            "+964 000 0000");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Same(expectedResult, result);
        Assert.NotNull(authenticationService.RegisteredBusiness);
        Assert.Equal("FinSight Coffee", authenticationService.RegisteredBusiness.Name);
        Assert.Equal("owner@example.com", authenticationService.Email);
        Assert.Equal("Password1!", authenticationService.Password);
    }

    private static RegistrationResult CreateResult()
    {
        return new RegistrationResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Owner",
            true);
    }

    private sealed class StubAuthenticationService(RegistrationResult result)
        : IAuthenticationService
    {
        public Business? RegisteredBusiness { get; private set; }

        public string? Email { get; private set; }

        public string? Password { get; private set; }

        public Task<RegistrationResult> RegisterOwnerAsync(
            Business business,
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            RegisteredBusiness = business;
            Email = email;
            Password = password;

            return Task.FromResult(result);
        }

        public Task<RegistrationResult> CreateManagerAsync(Guid businessId, string email, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AuthenticationResult> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeRefreshTokenAsync(Guid userId, string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
