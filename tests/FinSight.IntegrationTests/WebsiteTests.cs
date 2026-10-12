using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FinSight.Infrastructure.Identity;
using FinSight.Infrastructure.Persistence;
using FinSight.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinSight.Application.Common.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinSight.IntegrationTests;

public class WebsiteTests(FinSightApiFactory factory) : IClassFixture<FinSightApiFactory>
{
    private HttpClient Browser() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
    });

    [Fact]
    public async Task Owner_CanRegisterConfirmLoginCreateEditDeactivateReactivateAndLogout()
    {
        using var browser = Browser();
        var email = await RegisterAndConfirm(browser);
        var login = await Login(browser, email, "/products/create");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/products/create", login.Headers.Location?.OriginalString);
        var cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-FinSight.Auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);

        var name = $"Cappuccino-{Guid.NewGuid():N}";
        var creation = await Submit(browser, "/products/create", ProductFields(name));
        Assert.Equal(HttpStatusCode.Redirect, creation.StatusCode);
        var product = await FindProduct(name);
        var list = await browser.GetStringAsync("/products");
        Assert.Contains(name, list);
        Assert.Contains("Product added", list);

        var update = await Submit(browser, $"/products/{product.Id}/edit", ProductFields(name + " updated", "15.50"));
        Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);
        var updated = await FindProduct(name + " updated");
        Assert.Equal(15.50m, updated.SellingPrice);

        var deactivate = await Submit(browser, $"/products/{product.Id}/deactivate", [], "/products");
        Assert.Equal(HttpStatusCode.Redirect, deactivate.StatusCode);
        Assert.DoesNotContain(name + " updated", await browser.GetStringAsync("/products"));
        Assert.Contains(name + " updated", await browser.GetStringAsync("/products?includeInactive=true"));
        var reactivate = await Submit(browser, $"/products/{product.Id}/reactivate", [], "/products?includeInactive=true");
        Assert.Equal(HttpStatusCode.Redirect, reactivate.StatusCode);
        Assert.Contains(name + " updated", await browser.GetStringAsync("/products"));
        Assert.True((await FindProduct(name + " updated")).IsActive);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await browser.GetAsync("/account/logout")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, "/account/logout", [], "/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/products")).StatusCode);
    }

    [Fact]
    public async Task CookieAndJwt_AreRestrictedToTheirOwnSurfaces()
    {
        using var browser = Browser();
        var anonymousPage = await browser.GetAsync("/products");
        Assert.Equal(HttpStatusCode.Redirect, anonymousPage.StatusCode);
        Assert.Contains("/account/login", anonymousPage.Headers.Location!.OriginalString);
        var anonymousApi = await browser.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousApi.StatusCode);
        Assert.Equal("application/problem+json", anonymousApi.Content.Headers.ContentType?.MediaType);
        Assert.Null(anonymousApi.Headers.Location);

        var email = await RegisterAndConfirm(browser);
        Assert.Equal(HttpStatusCode.Redirect, (await Login(browser, email)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/products")).StatusCode);

        using var api = Browser();
        var token = await api.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Password1!" });
        token.EnsureSuccessStatusCode();
        var json = await token.Content.ReadFromJsonAsync<JsonElement>();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await api.GetAsync("/products")).StatusCode);
    }

    [Fact]
    public async Task WebsiteMutations_RequireAntiforgeryTokens()
    {
        using var browser = Browser();
        var email = await RegisterAndConfirm(browser);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Email"] = email, ["Password"] = "Password1!" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Login(browser, email)).StatusCode);
        foreach (var path in new[] { "/products/create", $"/products/{Guid.NewGuid()}/edit",
                     $"/products/{Guid.NewGuid()}/deactivate", $"/products/{Guid.NewGuid()}/reactivate", "/account/logout" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync(path, new FormUrlEncodedContent(ProductFields("Forgery")))).StatusCode);
        }
        using var anonymous = Browser();
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsync("/account/register", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsync("/account/confirm-email", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsync("/account/resend-confirmation", new FormUrlEncodedContent([]))).StatusCode);
    }

    [Fact]
    public async Task BusinessB_CannotReadOrModifyBusinessAProducts_AndSubmittedBusinessIdIsIgnored()
    {
        using var a = Browser();
        using var b = Browser();
        var emailA = await RegisterAndConfirm(a);
        var emailB = await RegisterAndConfirm(b);
        await Login(a, emailA);
        await Login(b, emailB);
        Assert.Contains("Add your first product", await a.GetStringAsync("/home"));
        var name = $"TenantA-{Guid.NewGuid():N}";
        await Submit(a, "/products/create", ProductFields(name));
        var product = await FindProduct(name);

        Assert.Contains("Manage products", await a.GetStringAsync("/home"));
        Assert.Contains("Add your first product", await b.GetStringAsync("/home"));
        Assert.DoesNotContain("Manage products", await b.GetStringAsync("/home"));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/simulations/preview?productId={product.Id}")).StatusCode);
        var preview = await Submit(a, "/simulations/preview", new()
        {
            ["ProductId"] = product.Id.ToString(), ["ProposedPrice"] = "15", ["CurrentSalesQuantity"] = "10",
            ["ExpectedSalesQuantity"] = "10"
        }, $"/simulations/preview?productId={product.Id}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Contains("Preview result", await preview.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<FinSightDbContext>().PricingSimulations.AnyAsync(s => s.ProductId == product.Id));

        Assert.DoesNotContain(name, await b.GetStringAsync("/products?includeInactive=true"));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/products/{product.Id}/edit")).StatusCode);
        foreach (var action in new[] { "edit", "deactivate", "reactivate" })
        {
            var result = await Submit(b, $"/products/{product.Id}/{action}", ProductFields("Hijacked"), "/products");
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }

        var forged = ProductFields($"TenantB-{Guid.NewGuid():N}");
        forged["BusinessId"] = product.BusinessId.ToString();
        await Submit(b, "/products/create", forged);
        var bProduct = await FindProduct(forged["Name"]);
        Assert.NotEqual(product.BusinessId, bProduct.BusinessId);
        Assert.True((await FindProduct(name)).IsActive);
    }

    [Fact]
    public async Task ProductForms_ShowValidationErrors_AndPaginationFiltersWork()
    {
        using var browser = Browser();
        await Login(browser, await RegisterAndConfirm(browser));
        var invalid = await Submit(browser, "/products/create", ProductFields("", "-1"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("validation-summary-errors", await invalid.Content.ReadAsStringAsync());
        var negative = await Submit(browser, "/products/create", ProductFields("Negative price", "-1"));
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Contains("greater than or equal", await negative.Content.ReadAsStringAsync());
        var suffix = Guid.NewGuid().ToString("N");
        await Submit(browser, "/products/create", ProductFields("A-" + suffix));
        await Submit(browser, "/products/create", ProductFields("B-" + suffix));
        var page1 = await browser.GetStringAsync("/products?page=1&pageSize=1");
        var page2 = await browser.GetStringAsync("/products?page=2&pageSize=1");
        Assert.Contains("Page 1 of 2", page1);
        Assert.Contains("Page 2 of 2", page2);
        Assert.Contains("A-" + suffix, page1);
        Assert.DoesNotContain("B-" + suffix, page1);
        Assert.Contains("B-" + suffix, page2);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.GetAsync("/products?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.GetAsync("/products?pageSize=101")).StatusCode);
        Assert.DoesNotContain("BusinessId", page1);
    }

    [Fact]
    public async Task UnconfirmedUser_CannotLogin_AndRegistrationValidationIsVisible()
    {
        using var browser = Browser();
        var email = await Register(browser);
        var login = await Login(browser, email);
        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
        Assert.Contains("confirm your email", await login.Content.ReadAsStringAsync());
        Assert.False(login.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.StartsWith("__Host-FinSight.Auth=")));
        var invalid = RegistrationFields("bad-email");
        invalid["ConfirmPassword"] = "different";
        var response = await Submit(browser, "/account/register", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("validation-summary-errors", html);
        Assert.DoesNotContain("value=\"Password1!\"", html);
    }

    [Fact]
    public async Task Manager_CanManageProducts_WithWebsiteCookie()
    {
        using var browser = Browser();
        var email = await RegisterAndConfirm(browser);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByEmailAsync(email))!;
            Assert.True((await users.RemoveFromRoleAsync(user, "Owner")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Manager")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await Login(browser, email)).StatusCode);
        var name = $"Manager-{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, "/products/create", ProductFields(name))).StatusCode);
        var product = await FindProduct(name);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, $"/products/{product.Id}/edit", ProductFields(name, "18"))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, $"/products/{product.Id}/deactivate", [], "/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Submit(browser, $"/products/{product.Id}/reactivate", [], "/products?includeInactive=true")).StatusCode);
    }

    [Fact]
    public async Task UnsupportedRole_GetsWebsiteAccessDenied_AndApi403WithoutRedirect()
    {
        using var browser = Browser();
        var email = await RegisterAndConfirm(browser);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(CustomClaimTypes.BusinessId, user.BusinessId.ToString()),
            new Claim(CustomClaimTypes.Role, "Auditor")
        };
        // Use real ticket protection and JWT validation, with a valid but unauthorized role.
        var cookieOptions = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme,
            JwtRegisteredClaimNames.Sub, CustomClaimTypes.Role));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10)
        }, IdentityConstants.ApplicationScheme);
        browser.DefaultRequestHeaders.Add("Cookie", $"{cookieOptions.Cookie.Name}={cookieOptions.TicketDataFormat.Protect(ticket)}");
        var page = await browser.GetAsync("/products");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Contains("/account/access-denied", page.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Forbidden, (await browser.GetAsync(page.Headers.Location)).StatusCode);

        var jwt = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(10), new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), SecurityAlgorithms.HmacSha256));
        browser.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            new JwtSecurityTokenHandler().WriteToken(token));
        var api = await browser.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Forbidden, api.StatusCode);
        Assert.Null(api.Headers.Location);
        Assert.Equal("application/problem+json", api.Content.Headers.ContentType?.MediaType);
    }

    private async Task<string> Register(HttpClient browser)
    {
        var email = $"web-{Guid.NewGuid():N}@example.com";
        var response = await Submit(browser, "/account/register", RegistrationFields(email));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/check-email", response.Headers.Location?.OriginalString);
        return email;
    }

    private async Task<string> RegisterAndConfirm(HttpClient browser)
    {
        var email = await Register(browser);
        var message = factory.EmailSender.Messages.Last(message => message.Recipient == email);
        var link = Regex.Match(message.Body, @"https://[^\s]+").Value;
        Assert.NotEmpty(link);
        var uri = new Uri(link);
        var entry = await browser.GetAsync(uri.PathAndQuery);
        Assert.Equal("/account/confirm-email", entry.Headers.Location!.OriginalString);
        var response = await Submit(browser, "/account/confirm-email", []);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/confirmation-success", response.Headers.Location!.OriginalString);
        return email;
    }

    private static Dictionary<string, string> RegistrationFields(string email) => new()
    {
        ["Email"] = email, ["Password"] = "Password1!", ["ConfirmPassword"] = "Password1!",
        ["BusinessName"] = "Website Café", ["Location"] = "Baghdad", ["Currency"] = "IQD",
        ["ContactInformation"] = email
    };

    private static Dictionary<string, string> ProductFields(string name, string price = "12") => new()
    {
        ["Name"] = name, ["Category"] = "Coffee", ["Size"] = "Regular",
        ["SellingPrice"] = price, ["UnitCost"] = "4"
    };

    private static Task<HttpResponseMessage> Login(HttpClient browser, string email, string returnUrl = "/products") =>
        Submit(browser, "/account/login", new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = "Password1!", ["ReturnUrl"] = returnUrl
        });

    private static async Task<HttpResponseMessage> Submit(HttpClient browser, string path,
        Dictionary<string, string> fields, string? formPage = null)
    {
        var page = await browser.GetAsync(formPage ?? path);
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        var token = WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(token);
        fields["__RequestVerificationToken"] = token;
        return await browser.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private async Task<FinSight.Domain.Entities.Product> FindProduct(string name)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<FinSightDbContext>()
            .Products.AsNoTracking().SingleAsync(product => product.Name == name);
    }
}
