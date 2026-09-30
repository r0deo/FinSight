using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinSight.Application.Common.Authorization;
using FinSight.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;

namespace FinSight.UnitTests.Infrastructure.Identity;

public class CurrentUserContextTests
{
    [Fact]
    public void Claims_WithRequiredValues_ReturnsCurrentUserData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var context = CreateContext(
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(CustomClaimTypes.BusinessId, businessId.ToString()),
            new Claim(CustomClaimTypes.Role, "Owner"));

        // Act
        var actualUserId = context.UserId;
        var actualBusinessId = context.BusinessId;
        var actualRole = context.Role;

        // Assert
        Assert.Equal(userId, actualUserId);
        Assert.Equal(businessId, actualBusinessId);
        Assert.Equal("Owner", actualRole);
    }

    [Fact]
    public void BusinessId_WithoutRequiredClaim_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var context = CreateContext(
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(CustomClaimTypes.Role, "Owner"));

        // Act
        Action act = () => _ = context.BusinessId;

        // Assert
        Assert.Throws<UnauthorizedAccessException>(act);
    }

    private static CurrentUserContext CreateContext(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "Bearer");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        var accessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        return new CurrentUserContext(accessor);
    }
}
