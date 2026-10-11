using FinSight.Application.Common.Authorization;
using FinSight.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace FinSight.Infrastructure.Identity;

public sealed class WebsiteAuthenticationService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IHttpContextAccessor httpContextAccessor) : IWebsiteAuthenticationService
{
    public async Task SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || user.BusinessId == Guid.Empty
            || !(await userManager.GetRolesAsync(user)).Any(AppRoles.All.Contains))
        {
            throw new UnauthorizedAccessException("Unable to sign in. Check your email and password and confirm your email first.");
        }

        var result = await signInManager.PasswordSignInAsync(
            user, password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            throw new UnauthorizedAccessException("Unable to sign in. Check your email and password and confirm your email first. If you tried several times, wait a few minutes.");
        }
    }

    public Task SignOutAsync() => httpContextAccessor.HttpContext!
        .SignOutAsync(IdentityConstants.ApplicationScheme);
}
