using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Identity;

public sealed class GoogleSignInService(
    SignInManager<ApplicationUser> signIn,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    FinSightDbContext database,
    IEmailConfirmationService confirmation,
    ICurrentUserContext currentUser,
    IHttpContextAccessor httpContext) : IGoogleSignInService
{
    public async Task BeginAsync(string localCallbackPath, bool linkToCurrentUser = false)
    {
        if (!localCallbackPath.StartsWith("/account/google/", StringComparison.Ordinal)
            || localCallbackPath.Contains('\\')) throw new ArgumentException("A local Google callback is required.");
        await ClearExternalAsync();
        var properties = signIn.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme,
            localCallbackPath, linkToCurrentUser ? currentUser.UserId.ToString() : null);
        properties.Items["flow"] = linkToCurrentUser ? "link" : "signin";
        await httpContext.HttpContext!.ChallengeAsync(GoogleDefaults.AuthenticationScheme, properties);
    }

    public async Task<GoogleSignInOutcome> CompleteSignInAsync(CancellationToken cancellationToken = default)
    {
        var info = await PendingAsync();
        if (info is null) return GoogleSignInOutcome.Failed;
        var user = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (user is not null) return await SignInExistingAsync(user, info);
        // A matching email is not proof of ownership of an existing FinSight account.
        if (await users.FindByEmailAsync(info.Principal.FindFirstValue(ClaimTypes.Email)!) is not null)
        {
            await ClearExternalAsync();
            return GoogleSignInOutcome.NeedsLocalSignIn;
        }
        return GoogleSignInOutcome.NeedsBusiness;
    }

    public async Task<GoogleIdentitySummary?> GetPendingIdentityAsync()
    {
        var info = await PendingAsync();
        return info is null ? null : new GoogleIdentitySummary(info.Principal.FindFirstValue(ClaimTypes.Email)!);
    }

    public async Task<GoogleSignInOutcome> CreateBusinessAsync(Business business, CancellationToken cancellationToken = default)
    {
        try { return await CreateBusinessCoreAsync(business, cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent local registration or different Google subject can claim the email.
            // The transaction has rolled back; never link by email or retain an orphan Business.
            await ClearExternalAsync();
            return GoogleSignInOutcome.NeedsLocalSignIn;
        }
    }

    private async Task<GoogleSignInOutcome> CreateBusinessCoreAsync(Business business, CancellationToken cancellationToken)
    {
        var info = await PendingAsync();
        if (info is null) return GoogleSignInOutcome.Failed;
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        // Serialize concurrent retries for this provider subject across application instances.
        var key = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes($"Google:{info.ProviderKey}")), 0);
        await database.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})", cancellationToken);
        var existing = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return await SignInExistingAsync(existing, info);
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email)!;
        if (await users.FindByEmailAsync(email) is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            await ClearExternalAsync();
            return GoogleSignInOutcome.NeedsLocalSignIn;
        }

        await IdentityRoleProvisioner.EnsureAsync(database, roles, cancellationToken);
        database.Businesses.Add(business);
        await database.SaveChangesAsync(cancellationToken);
        var user = new ApplicationUser(Guid.NewGuid(), business.Id, email)
        {
            EmailConfirmed = string.Equals(info.Principal.FindFirstValue(GoogleSignInOptions.VerifiedEmailClaim), "true", StringComparison.OrdinalIgnoreCase)
        };
        ThrowIfFailed(await users.CreateAsync(user));
        ThrowIfFailed(await users.AddToRoleAsync(user, AppRoles.Owner));
        ThrowIfFailed(await users.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, "Google")));
        await transaction.CommitAsync(cancellationToken);
        if (!user.EmailConfirmed) await confirmation.SendAsync(user.Id, cancellationToken);
        return await SignInExistingAsync(user, info);
    }

    public async Task<bool> LinkToCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        // XSRF binds the external round trip to the already-authenticated account that initiated it.
        var info = await signIn.GetExternalLoginInfoAsync(userId.ToString());
        if (info is null || info.LoginProvider != GoogleDefaults.AuthenticationScheme
            || !HasFlow(info, "link"))
        {
            await ClearExternalAsync();
            return false;
        }
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.EmailConfirmed || user.BusinessId != currentUser.BusinessId)
        {
            await ClearExternalAsync();
            return false;
        }
        var existing = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (existing is not null)
        {
            await ClearExternalAsync();
            return existing.Id == user.Id;
        }
        IdentityResult result;
        try { result = await users.AddLoginAsync(user, new UserLoginInfo(info.LoginProvider, info.ProviderKey, "Google")); }
        catch (DbUpdateException) { await ClearExternalAsync(); return false; }
        await ClearExternalAsync();
        return result.Succeeded;
    }

    private async Task<ExternalLoginInfo?> PendingAsync()
    {
        var info = await signIn.GetExternalLoginInfoAsync();
        if (info is null || info.LoginProvider != GoogleDefaults.AuthenticationScheme
            || !HasFlow(info, "signin")
            || string.IsNullOrWhiteSpace(info.ProviderKey)
            || !System.Net.Mail.MailAddress.TryCreate(info.Principal.FindFirstValue(ClaimTypes.Email), out _)) return null;
        return info;
    }

    private async Task<GoogleSignInOutcome> SignInExistingAsync(ApplicationUser user, ExternalLoginInfo info)
    {
        if (!user.EmailConfirmed)
        {
            await ClearExternalAsync();
            return GoogleSignInOutcome.NeedsConfirmation;
        }
        if (user.BusinessId == Guid.Empty || !(await users.GetRolesAsync(user)).Any(AppRoles.All.Contains))
        {
            await ClearExternalAsync();
            return GoogleSignInOutcome.Failed;
        }
        var result = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey,
            isPersistent: false, bypassTwoFactor: false);
        await ClearExternalAsync();
        return result.Succeeded ? GoogleSignInOutcome.SignedIn : GoogleSignInOutcome.Failed;
    }

    private Task ClearExternalAsync() => httpContext.HttpContext!.SignOutAsync(IdentityConstants.ExternalScheme);

    private static bool HasFlow(ExternalLoginInfo info, string expected) =>
        info.AuthenticationProperties?.Items.TryGetValue("flow", out var flow) == true && flow == expected;

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new RequestValidationException(new Dictionary<string, string[]>
            { ["Account"] = ["We couldn't complete account setup. If you already have an account, sign in to it before linking Google."] });
    }
}
