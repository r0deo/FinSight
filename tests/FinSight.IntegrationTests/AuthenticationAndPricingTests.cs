using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinSight.IntegrationTests.Infrastructure;

namespace FinSight.IntegrationTests;

public class AuthenticationAndPricingTests(FinSightApiFactory factory)
    : IClassFixture<FinSightApiFactory>
{
    [Fact]
    public async Task RegisteredOwner_CanCreateProductAndPreviewPricing()
    {
        var client = factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@example.com";

        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Password1!",
            BusinessName = "Integration Business",
            Location = "Baghdad",
            Currency = "USD",
            ContactInformation = email
        });
        registration.EnsureSuccessStatusCode();
        var registrationJson = await registration.Content.ReadFromJsonAsync<JsonElement>();
        var userId = registrationJson.GetProperty("userId").GetGuid();
        var confirmationEmail = factory.EmailSender.Messages.Last(message =>
            message.Recipient == email && message.Subject.Contains("Confirm"));
        var confirmationToken = confirmationEmail.Body.Split("Confirmation token: ")[1];

        var confirmation = await client.PostAsJsonAsync("/api/auth/confirm-email", new
        {
            UserId = userId,
            Token = confirmationToken
        });
        Assert.Equal(HttpStatusCode.NoContent, confirmation.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "Password1!"
        });
        login.EnsureSuccessStatusCode();
        var loginJson = await login.Content.ReadFromJsonAsync<JsonElement>();
        var ownerAccessToken = loginJson.GetProperty("accessToken").GetString();
        var initialRefreshToken = loginJson.GetProperty("refreshToken").GetString();

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = initialRefreshToken
        });
        refresh.EnsureSuccessStatusCode();
        var refreshJson = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        var rotatedRefreshToken = refreshJson.GetProperty("refreshToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            refreshJson.GetProperty("accessToken").GetString());

        var revocation = await client.PostAsJsonAsync("/api/auth/revoke", new
        {
            RefreshToken = rotatedRefreshToken
        });
        Assert.Equal(HttpStatusCode.NoContent, revocation.StatusCode);
        var rejectedRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            RefreshToken = rotatedRefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedRefresh.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ownerAccessToken);

        var managerEmail = $"manager-{Guid.NewGuid():N}@example.com";
        var managerCreation = await client.PostAsJsonAsync("/api/auth/managers", new
        {
            Email = managerEmail,
            Password = "Password1!"
        });
        managerCreation.EnsureSuccessStatusCode();
        var managerJson = await managerCreation.Content.ReadFromJsonAsync<JsonElement>();
        var managerId = managerJson.GetProperty("userId").GetGuid();
        var managerEmailMessage = factory.EmailSender.Messages.Last(message =>
            message.Recipient == managerEmail && message.Subject.Contains("Confirm"));
        var managerConfirmationToken = managerEmailMessage.Body.Split("Confirmation token: ")[1];
        var managerConfirmation = await client.PostAsJsonAsync("/api/auth/confirm-email", new
        {
            UserId = managerId,
            Token = managerConfirmationToken
        });
        managerConfirmation.EnsureSuccessStatusCode();
        var managerLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = managerEmail,
            Password = "Password1!"
        });
        managerLogin.EnsureSuccessStatusCode();
        var managerLoginJson = await managerLogin.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Manager", managerLoginJson.GetProperty("role").GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            managerLoginJson.GetProperty("accessToken").GetString());

        var creation = await client.PostAsJsonAsync("/api/products", new
        {
            Name = "Coffee",
            Category = "Drinks",
            Size = "Large",
            SellingPrice = 10m,
            UnitCost = 4m
        });
        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
        var productJson = await creation.Content.ReadFromJsonAsync<JsonElement>();
        var productId = productJson.GetProperty("id").GetGuid();

        var preview = await client.PostAsJsonAsync("/api/pricing-simulations/preview", new
        {
            ProductId = productId,
            ProposedPrice = 12m,
            CurrentSalesQuantity = 5,
            ExpectedSalesQuantity = 6
        });
        preview.EnsureSuccessStatusCode();
        var previewJson = await preview.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(30m, previewJson.GetProperty("currentContribution").GetDecimal());
        Assert.Equal(48m, previewJson.GetProperty("proposedContribution").GetDecimal());
        Assert.Equal("Recommended", previewJson.GetProperty("recommendation").GetString());
    }

    [Fact]
    public async Task Preview_WithoutAuthentication_ReturnsUnauthorizedProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/pricing-simulations/preview", new
        {
            ProductId = Guid.NewGuid(),
            ProposedPrice = 12m,
            CurrentSalesQuantity = 5,
            ExpectedSalesQuantity = 6
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ConfirmedUser_CanResetPasswordAndLoginWithNewPassword()
    {
        var client = factory.CreateClient();
        var email = $"recovery-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Password1!",
            BusinessName = "Recovery Business",
            Location = "Baghdad",
            Currency = "USD",
            ContactInformation = email
        });
        registration.EnsureSuccessStatusCode();
        var registrationJson = await registration.Content.ReadFromJsonAsync<JsonElement>();
        var userId = registrationJson.GetProperty("userId").GetGuid();
        var confirmationMessage = factory.EmailSender.Messages.Last(message =>
            message.Recipient == email && message.Subject.Contains("Confirm"));
        var confirmationToken = confirmationMessage.Body.Split("Confirmation token: ")[1];
        var confirmation = await client.PostAsJsonAsync("/api/auth/confirm-email", new
        {
            UserId = userId,
            Token = confirmationToken
        });
        confirmation.EnsureSuccessStatusCode();

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new
        {
            Email = email
        });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        var resetMessage = factory.EmailSender.Messages.Last(message =>
            message.Recipient == email && message.Subject.Contains("Reset"));
        var resetToken = resetMessage.Body.Split("Reset token: ")[1];
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new
        {
            UserId = userId,
            Token = resetToken,
            NewPassword = "NewPassword1!"
        });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "Password1!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "NewPassword1!"
        });
        newLogin.EnsureSuccessStatusCode();
    }
}
