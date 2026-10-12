using FinSight.Api.Website;

namespace FinSight.UnitTests.Website;

public class PostAuthenticationDestinationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/products/../account/login")]
    [InlineData("/products/%2e%2e/account/login")]
    [InlineData("/account/google/callback")]
    [InlineData("/account/google/business")]
    [InlineData("/account/logout")]
    [InlineData("/api/products")]
    public void UnsafeOrNonResumableReturn_DefaultsToHome(string? url) =>
        Assert.Equal("/home", PostAuthenticationDestination.Resolve(false, url));

    [Theory]
    [InlineData("/products?page=2&includeInactive=true")]
    [InlineData("/products/create")]
    [InlineData("/account/settings")]
    [InlineData("/simulations/preview?productId=32dca37d-71c4-4db0-a60a-b238c43d22ea")]
    public void ValidWebsiteReturn_IsPreserved(string url) =>
        Assert.Equal(url, PostAuthenticationDestination.Resolve(false, url));

    [Fact]
    public void MissingBusiness_TakesPrecedenceOverReturnUrl() =>
        Assert.Equal("/account/google/business", PostAuthenticationDestination.Resolve(true, "/products/create"));
}
