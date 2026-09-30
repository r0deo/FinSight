using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinSight.Application.Common.Authorization;
using FinSight.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FinSight.Infrastructure.Identity;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserContext
{
    public Guid UserId => GetRequiredGuidClaim(JwtRegisteredClaimNames.Sub);

    public Guid BusinessId => GetRequiredGuidClaim(CustomClaimTypes.BusinessId);

    public string Role => GetRequiredClaim(CustomClaimTypes.Role);

    private ClaimsPrincipal GetAuthenticatedUser()
    {
        var user = httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("An authenticated user is required.");
        }

        return user;
    }

    private string GetRequiredClaim(string claimType)
    {
        var value = GetAuthenticatedUser().FindFirstValue(claimType);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UnauthorizedAccessException(
                $"The required '{claimType}' claim is unavailable.");
        }

        return value;
    }

    private Guid GetRequiredGuidClaim(string claimType)
    {
        var value = GetRequiredClaim(claimType);

        if (!Guid.TryParse(value, out var identifier)
            || identifier == Guid.Empty)
        {
            throw new UnauthorizedAccessException(
                $"The required '{claimType}' claim is invalid.");
        }

        return identifier;
    }
}
