using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinSight.IntegrationTests.Infrastructure;

namespace FinSight.IntegrationTests;

public class SavedPricingSimulationTests(FinSightApiFactory factory)
    : IClassFixture<FinSightApiFactory>
{
    [Fact]
    public async Task SavedSimulation_CanBeRetrievedAndKeepsProductSnapshot()
    {
        var client = factory.CreateClient();
        var token = await RegisterConfirmAndLoginAsync(
            client,
            $"snapshot-{Guid.NewGuid():N}@example.com",
            "Snapshot Business");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token);
        var productId = await CreateProductAsync(client, 10m, 4m);

        var creation = await client.PostAsJsonAsync("/api/pricing-simulations", new
        {
            ProductId = productId,
            ProposedPrice = 12m,
            CurrentSalesQuantity = 5,
            ExpectedSalesQuantity = 6
        });

        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
        Assert.NotNull(creation.Headers.Location);
        var created = await creation.Content.ReadFromJsonAsync<JsonElement>();
        var simulationId = created.GetProperty("id").GetGuid();
        Assert.Equal(10m, created.GetProperty("currentPrice").GetDecimal());
        Assert.Equal(4m, created.GetProperty("unitCost").GetDecimal());
        Assert.Equal(18m, created.GetProperty("difference").GetDecimal());

        var productUpdate = await client.PutAsJsonAsync($"/api/products/{productId}", new
        {
            Name = "Coffee",
            Category = "Drinks",
            Size = "Large",
            SellingPrice = 30m,
            UnitCost = 20m
        });
        Assert.Equal(HttpStatusCode.NoContent, productUpdate.StatusCode);

        var retrieval = await client.GetAsync($"/api/pricing-simulations/{simulationId}");
        retrieval.EnsureSuccessStatusCode();
        var retrieved = await retrieval.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10m, retrieved.GetProperty("currentPrice").GetDecimal());
        Assert.Equal(4m, retrieved.GetProperty("unitCost").GetDecimal());
        Assert.Equal(30m, retrieved.GetProperty("currentContribution").GetDecimal());
        Assert.Equal(48m, retrieved.GetProperty("proposedContribution").GetDecimal());

        var history = await client.GetFromJsonAsync<JsonElement>(
            "/api/pricing-simulations?page=1&pageSize=20");
        Assert.Contains(
            history.GetProperty("items").EnumerateArray(),
            simulation => simulation.GetProperty("id").GetGuid() == simulationId);
    }

    [Fact]
    public async Task Simulation_FromAnotherBusiness_CannotBeReadOrUpdated()
    {
        var ownerClient = factory.CreateClient();
        var ownerToken = await RegisterConfirmAndLoginAsync(
            ownerClient,
            $"tenant-a-{Guid.NewGuid():N}@example.com",
            "Tenant A");
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ownerToken);
        var productId = await CreateProductAsync(ownerClient, 10m, 4m);
        var creation = await ownerClient.PostAsJsonAsync("/api/pricing-simulations", new
        {
            ProductId = productId,
            ProposedPrice = 12m,
            CurrentSalesQuantity = 5,
            ExpectedSalesQuantity = 6
        });
        creation.EnsureSuccessStatusCode();
        var created = await creation.Content.ReadFromJsonAsync<JsonElement>();
        var simulationId = created.GetProperty("id").GetGuid();

        var otherTenantClient = factory.CreateClient();
        var otherTenantToken = await RegisterConfirmAndLoginAsync(
            otherTenantClient,
            $"tenant-b-{Guid.NewGuid():N}@example.com",
            "Tenant B");
        otherTenantClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            otherTenantToken);

        var retrieval = await otherTenantClient.GetAsync(
            $"/api/pricing-simulations/{simulationId}");
        var update = await otherTenantClient.PutAsJsonAsync(
            $"/api/pricing-simulations/{simulationId}",
            new
            {
                ProposedPrice = 20m,
                CurrentSalesQuantity = 5,
                ExpectedSalesQuantity = 10
            });

        Assert.Equal(HttpStatusCode.NotFound, retrieval.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    private async Task<string> RegisterConfirmAndLoginAsync(
        HttpClient client,
        string email,
        string businessName)
    {
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = email,
            Password = "Password1!",
            BusinessName = businessName,
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
        confirmation.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = "Password1!"
        });
        login.EnsureSuccessStatusCode();
        var loginJson = await login.Content.ReadFromJsonAsync<JsonElement>();
        return loginJson.GetProperty("accessToken").GetString()!;
    }

    private static async Task<Guid> CreateProductAsync(
        HttpClient client,
        decimal sellingPrice,
        decimal unitCost)
    {
        var response = await client.PostAsJsonAsync("/api/products", new
        {
            Name = "Coffee",
            Category = "Drinks",
            Size = "Large",
            SellingPrice = sellingPrice,
            UnitCost = unitCost
        });
        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<JsonElement>();
        return product.GetProperty("id").GetGuid();
    }
}
