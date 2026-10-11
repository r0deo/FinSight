using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinSight.Application.Common.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace FinSight.Infrastructure.Identity;

public sealed class WebsiteClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()));
        identity.AddClaim(new Claim(CustomClaimTypes.BusinessId, user.BusinessId.ToString()));
        foreach (var role in await UserManager.GetRolesAsync(user))
        {
            if (AppRoles.All.Contains(role))
            {
                identity.AddClaim(new Claim(CustomClaimTypes.Role, role));
            }
        }
        return identity;
    }
}
