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
        factory.TimeProvider.SetUtcNow(DateTimeOffset.UtcNow);
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
        factory.TimeProvider.SetUtcNow(DateTimeOffset.UtcNow);
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
        var simulationVersion = created.GetProperty("version").GetInt64();

        var otherTenantClient = factory.CreateClient();
        var otherTenantToken = await RegisterConfirmAndLoginAsync(
            otherTenantClient,
            $"tenant-b-{Guid.NewGuid():N}@example.com",
            "Tenant B");
        otherTenantClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            otherTenantToken);

        var ownHistory = await otherTenantClient.GetAsync("/api/pricing-simulations");
        Assert.Equal(HttpStatusCode.OK, ownHistory.StatusCode);

        var retrieval = await otherTenantClient.GetAsync(
            $"/api/pricing-simulations/{simulationId}");
        var update = await otherTenantClient.PutAsJsonAsync(
            $"/api/pricing-simulations/{simulationId}",
            new
            {
                ProposedPrice = 20m,
                CurrentSalesQuantity = 5,
                ExpectedSalesQuantity = 10,
                Version = simulationVersion
            });
        var restore = await otherTenantClient.PatchAsJsonAsync(
            $"/api/pricing-simulations/{simulationId}/restore",
            new { Version = simulationVersion });

        Assert.Equal(HttpStatusCode.NotFound, retrieval.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, restore.StatusCode);
    }

    [Fact]
    public async Task SameVersionUpdates_OneSucceedsAndOneConflicts_AndArchiveFiltersHistory()
    {
        factory.TimeProvider.SetUtcNow(DateTimeOffset.UtcNow);
        var client = factory.CreateClient();
        var token = await RegisterConfirmAndLoginAsync(
            client,
            $"concurrency-{Guid.NewGuid():N}@example.com",
            "Concurrency Business");
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
        creation.EnsureSuccessStatusCode();
        var created = await creation.Content.ReadFromJsonAsync<JsonElement>();
        var simulationId = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetInt64();

        var firstUpdate = client.PutAsJsonAsync($"/api/pricing-simulations/{simulationId}", new
        {
            ProposedPrice = 13m,
            CurrentSalesQuantity = 5,
            ExpectedSalesQuantity = 7,
            Version = version
        });
        var secondUpdate = client.PutAsJsonAsync($"/api/pricing-simulations/{simulationId}", new
        {
            ProposedPrice = 14m,
            CurrentSalesQuantity = 5,
            ExpectedSalesQuantity = 8,
            Version = version
        });
        var updateResponses = await Task.WhenAll(firstUpdate, secondUpdate);

        Assert.Single(updateResponses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(updateResponses, response => response.StatusCode == HttpStatusCode.Conflict);
        var successfulUpdate = updateResponses.Single(
            response => response.StatusCode == HttpStatusCode.OK);
        var updateResult = await successfulUpdate.Content.ReadFromJsonAsync<JsonElement>();
        var updatedVersion = updateResult.GetProperty("version").GetInt64();
        var beforeArchive = await client.GetFromJsonAsync<JsonElement>(
            $"/api/pricing-simulations/{simulationId}");

        var archive = await client.PatchAsJsonAsync(
            $"/api/pricing-simulations/{simulationId}/archive",
            new { Version = updatedVersion });
        archive.EnsureSuccessStatusCode();
        var archiveResult = await archive.Content.ReadFromJsonAsync<JsonElement>();
        var archivedVersion = archiveResult.GetProperty("version").GetInt64();
        var defaultHistory = await client.GetFromJsonAsync<JsonElement>(
            "/api/pricing-simulations?page=1&pageSize=20");
        var archivedHistory = await client.GetFromJsonAsync<JsonElement>(
            "/api/pricing-simulations?page=1&pageSize=20&includeArchived=true");
        var archivedRetrieval = await client.GetAsync(
            $"/api/pricing-simulations/{simulationId}");

        Assert.Equal(HttpStatusCode.OK, archivedRetrieval.StatusCode);
        var archivedSnapshot = await archivedRetrieval.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(archivedSnapshot.GetProperty("isArchived").GetBoolean());
        AssertCalculationUnchanged(beforeArchive, archivedSnapshot);
        Assert.DoesNotContain(
            defaultHistory.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == simulationId);
        Assert.Contains(
            archivedHistory.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == simulationId);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PutAsJsonAsync($"/api/pricing-simulations/{simulationId}", new
            {
                ProposedPrice = 15m,
                CurrentSalesQuantity = 5,
                ExpectedSalesQuantity = 9,
                Version = archivedVersion
            })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PatchAsJsonAsync(
                $"/api/pricing-simulations/{simulationId}/archive",
                new { Version = archivedVersion })).StatusCode);

        var unarchive = await client.PatchAsJsonAsync(
            $"/api/pricing-simulations/{simulationId}/unarchive",
            new { Version = archivedVersion });
        unarchive.EnsureSuccessStatusCode();
        var unarchiveResult = await unarchive.Content.ReadFromJsonAsync<JsonElement>();
        var unarchivedSnapshot = await client.GetFromJsonAsync<JsonElement>(
            $"/api/pricing-simulations/{simulationId}");
        Assert.False(unarchivedSnapshot.GetProperty("isArchived").GetBoolean());
        AssertCalculationUnchanged(beforeArchive, unarchivedSnapshot);
        var unarchivedHistory = await client.GetFromJsonAsync<JsonElement>(
            "/api/pricing-simulations?page=1&pageSize=20");
        Assert.Contains(
            unarchivedHistory.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == simulationId);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PatchAsJsonAsync(
                $"/api/pricing-simulations/{simulationId}/unarchive",
                new { Version = unarchiveResult.GetProperty("version").GetInt64() })).StatusCode);
    }

    [Fact]
    public async Task Restore_AtThirtyDaysSucceeds_AfterThirtyDaysConflicts()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var baseline = new DateTimeOffset(
            utcNow.Ticks - (utcNow.Ticks % TimeSpan.TicksPerMillisecond),
            TimeSpan.Zero);
        factory.TimeProvider.SetUtcNow(baseline);

        try
        {
            var client = factory.CreateClient();
            var token = await RegisterConfirmAndLoginAsync(
                client,
                $"restore-{Guid.NewGuid():N}@example.com",
                "Restore Business");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token);
            var productId = await CreateProductAsync(client, 10m, 4m);
            var firstSimulation = await CreateSimulationAsync(client, productId);
            var secondSimulation = await CreateSimulationAsync(client, productId);

            var firstDelete = await SendDeleteAsync(
                client,
                firstSimulation.Id,
                firstSimulation.Version);
            firstDelete.EnsureSuccessStatusCode();
            var firstDeleteResult = await firstDelete.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await client.GetAsync(
                    $"/api/pricing-simulations/{firstSimulation.Id}")).StatusCode);
            factory.TimeProvider.SetUtcNow(baseline.AddDays(30));
            var boundaryRestore = await client.PatchAsJsonAsync(
                $"/api/pricing-simulations/{firstSimulation.Id}/restore",
                new { Version = firstDeleteResult.GetProperty("version").GetInt64() });

            Assert.Equal(HttpStatusCode.OK, boundaryRestore.StatusCode);
            Assert.Equal(
                HttpStatusCode.OK,
                (await client.GetAsync(
                    $"/api/pricing-simulations/{firstSimulation.Id}")).StatusCode);

            var secondDelete = await SendDeleteAsync(
                client,
                secondSimulation.Id,
                secondSimulation.Version);
            secondDelete.EnsureSuccessStatusCode();
            var secondDeleteResult = await secondDelete.Content.ReadFromJsonAsync<JsonElement>();
            var deletedVersion = secondDeleteResult.GetProperty("version").GetInt64();
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await client.GetAsync(
                    $"/api/pricing-simulations/{secondSimulation.Id}")).StatusCode);
            Assert.Equal(
                HttpStatusCode.Conflict,
                (await SendDeleteAsync(client, secondSimulation.Id, deletedVersion)).StatusCode);

            factory.TimeProvider.SetUtcNow(baseline.AddDays(60).AddTicks(1));
            var expiredRestore = await client.PatchAsJsonAsync(
                $"/api/pricing-simulations/{secondSimulation.Id}/restore",
                new { Version = deletedVersion });

            Assert.Equal(HttpStatusCode.Conflict, expiredRestore.StatusCode);
        }
        finally
        {
            factory.TimeProvider.SetUtcNow(DateTimeOffset.UtcNow);
        }
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
        var confirmationToken = confirmationEmail.ConfirmationToken;
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
        Assert.Equal("Owner", loginJson.GetProperty("role").GetString());
        return loginJson.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Update_WithPreviouslyReadVersion_RejectsSequentialStaleRetry()
    {
        // Arrange
        factory.TimeProvider.SetUtcNow(DateTimeOffset.UtcNow);
        using var client = factory.CreateClient();
        var token = await RegisterConfirmAndLoginAsync(
            client,
            $"stale-version-{Guid.NewGuid():N}@example.com",
            "Stale Version Business");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var productId = await CreateProductAsync(client, 10m, 4m);
        var simulation = await CreateSimulationAsync(client, productId);
        var url = $"/api/pricing-simulations/{simulation.Id}";
        var original = await client.GetFromJsonAsync<JsonElement>(url);
        var oldVersion = original.GetProperty("version").GetInt64();

        // Act
        var firstUpdate = await client.PutAsJsonAsync(url, new
        {
            ProposedPrice = 13m,
            CurrentSalesQuantity = 5,
            ExpectedSalesQuantity = 7,
            Version = oldVersion
        });
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);
        var accepted = await firstUpdate.Content.ReadFromJsonAsync<JsonElement>();
        var secondUpdate = await client.PutAsJsonAsync(url, new
        {
            ProposedPrice = 99m,
            CurrentSalesQuantity = 5,
            ExpectedSalesQuantity = 100,
            Version = oldVersion
        });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, secondUpdate.StatusCode);
        Assert.Equal("application/problem+json", secondUpdate.Content.Headers.ContentType?.MediaType);
        var problem = await secondUpdate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(409, problem.GetProperty("status").GetInt32());
        var persisted = await client.GetFromJsonAsync<JsonElement>(url);
        Assert.Equal(oldVersion + 1, accepted.GetProperty("version").GetInt64());
        Assert.Equal(oldVersion + 1, persisted.GetProperty("version").GetInt64());
        Assert.Equal(13m, persisted.GetProperty("proposedPrice").GetDecimal());
        Assert.Equal(63m, persisted.GetProperty("proposedContribution").GetDecimal());
    }

    private static void AssertCalculationUnchanged(JsonElement before, JsonElement after)
    {
        var calculationFields = new[]
        {
            "currentPrice", "unitCost", "proposedPrice",
            "currentSalesQuantity", "expectedSalesQuantity",
            "currentContribution", "proposedContribution", "difference",
            "isProposedPriceBelowCost", "recommendation"
        };

        foreach (var field in calculationFields)
        {
            Assert.Equal(before.GetProperty(field).GetRawText(), after.GetProperty(field).GetRawText());
        }
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

    private static async Task<(Guid Id, long Version)> CreateSimulationAsync(
        HttpClient client,
        Guid productId)
    {
        var response = await client.PostAsJsonAsync("/api/pricing-simulations", new
        {
            ProductId = productId,
            ProposedPrice = 12m,
            CurrentSalesQuantity = 5,
            ExpectedSalesQuantity = 6
        });
        response.EnsureSuccessStatusCode();
        var simulation = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (
            simulation.GetProperty("id").GetGuid(),
            simulation.GetProperty("version").GetInt64());
    }

    private static Task<HttpResponseMessage> SendDeleteAsync(
        HttpClient client,
        Guid simulationId,
        long version)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/pricing-simulations/{simulationId}")
        {
            Content = JsonContent.Create(new { Version = version })
        };
        return client.SendAsync(request);
    }
}
