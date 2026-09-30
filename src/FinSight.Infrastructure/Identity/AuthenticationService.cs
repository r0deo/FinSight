using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinSight.Infrastructure.Identity;

public sealed class AuthenticationService(
    FinSightDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IEmailSender emailSender,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : IAuthenticationService
{
    public async Task<RegistrationResult> RegisterOwnerAsync(
        Business business,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        await EnsureRolesExistAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Businesses.Add(business);
        await dbContext.SaveChangesAsync(cancellationToken);
        var user = await CreateUserAsync(business.Id, email, password, AppRoles.Owner, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await SendConfirmationEmailAsync(user, cancellationToken);

        return new RegistrationResult(user.Id, business.Id, AppRoles.Owner, true);
    }

    public async Task<RegistrationResult> CreateManagerAsync(
        Guid businessId,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Businesses.AnyAsync(
                business => business.Id == businessId,
                cancellationToken))
        {
            throw new NotFoundException(nameof(Business), businessId);
        }

        await EnsureRolesExistAsync(cancellationToken);
        var user = await CreateUserAsync(
            businessId,
            email,
            password,
            AppRoles.Manager,
            cancellationToken);
        await SendConfirmationEmailAsync(user, cancellationToken);

        return new RegistrationResult(user.Id, businessId, AppRoles.Manager, true);
    }

    public async Task<AuthenticationResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email.Trim());

        if (user is null
            || !user.EmailConfirmed
            || !await userManager.CheckPasswordAsync(user, password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        return await IssueTokensAsync(user, await GetSupportedRoleAsync(user), cancellationToken);
    }

    public async Task<AuthenticationResult> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = HashToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens.SingleOrDefaultAsync(
            token => token.TokenHash == tokenHash,
            cancellationToken);

        if (storedToken is null
            || storedToken.RevokedAtUtc is not null
            || storedToken.ExpiresAtUtc <= now)
        {
            throw new UnauthorizedAccessException("The refresh token is invalid or expired.");
        }

        var user = await userManager.FindByIdAsync(storedToken.UserId.ToString())
            ?? throw new UnauthorizedAccessException("The refresh token is invalid.");

        storedToken.Revoke(now);
        return await IssueTokensAsync(user, await GetSupportedRoleAsync(user), cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens.SingleOrDefaultAsync(
            token => token.TokenHash == tokenHash && token.UserId == userId,
            cancellationToken);

        if (storedToken is null || storedToken.RevokedAtUtc is not null)
        {
            return;
        }

        storedToken.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        var result = await userManager.ConfirmEmailAsync(user, token);
        ThrowIfFailed(result);
    }

    public async Task RequestPasswordResetAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email.Trim());

        if (user is null || !user.EmailConfirmed)
        {
            return;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        await emailSender.SendAsync(
            user.Email!,
            "Reset your FinSight password",
            $"User ID: {user.Id}\nReset token: {token}",
            cancellationToken);
    }

    public async Task ResetPasswordAsync(
        Guid userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException(nameof(ApplicationUser), userId);
        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        ThrowIfFailed(result);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activeTokens = await dbContext.RefreshTokens
            .Where(refreshToken => refreshToken.UserId == userId
                && refreshToken.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        activeTokens.ForEach(refreshToken => refreshToken.Revoke(now));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ApplicationUser> CreateUserAsync(
        Guid businessId,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = new ApplicationUser(Guid.NewGuid(), businessId, email);
        ThrowIfFailed(await userManager.CreateAsync(user, password));
        ThrowIfFailed(await userManager.AddToRoleAsync(user, role));
        return user;
    }

    private async Task SendConfirmationEmailAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailSender.SendAsync(
            user.Email!,
            "Confirm your FinSight email",
            $"User ID: {user.Id}\nConfirmation token: {token}",
            cancellationToken);
    }

    private async Task EnsureRolesExistAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                ThrowIfFailed(await roleManager.CreateAsync(new IdentityRole<Guid>(role)));
            }
        }
    }

    private async Task<string> GetSupportedRoleAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return roles.FirstOrDefault(AppRoles.All.Contains)
            ?? throw new UnauthorizedAccessException("The user has no supported role.");
    }

    private async Task<AuthenticationResult> IssueTokensAsync(
        ApplicationUser user,
        string role,
        CancellationToken cancellationToken)
    {
        var options = jwtOptions.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var accessTokenExpiresAtUtc = now.AddMinutes(options.ExpirationMinutes);
        var refreshTokenExpiresAtUtc = now.AddDays(options.RefreshTokenExpirationDays);
        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(CustomClaimTypes.BusinessId, user.BusinessId.ToString()),
            new Claim(CustomClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            now,
            accessTokenExpiresAtUtc,
            new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        dbContext.RefreshTokens.Add(new RefreshToken(
            Guid.NewGuid(),
            user.Id,
            HashToken(rawRefreshToken),
            refreshTokenExpiresAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthenticationResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            accessTokenExpiresAtUtc,
            rawRefreshToken,
            refreshTokenExpiresAtUtc,
            user.Id,
            user.BusinessId,
            role);
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new RequestValidationException(result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray()));
    }
}
