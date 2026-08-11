using System.Net;
using System.Text.Json;
using PulsePilot.IntegrationTests.Persistence;

namespace PulsePilot.IntegrationTests.Api;

public sealed class ApiInfrastructureTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task InfrastructureEndpoints_ReturnExpectedResponses()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        using var liveResponse = await client.GetAsync("/health/live");
        using var readyResponse = await client.GetAsync("/health/ready");
        using var swaggerResponse = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, swaggerResponse.StatusCode);

        using var liveDocument = JsonDocument.Parse(await liveResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Healthy", liveDocument.RootElement.GetProperty("status").GetString());

        using var readyDocument = JsonDocument.Parse(await readyResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Healthy", readyDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal(
            "Healthy",
            readyDocument.RootElement
                .GetProperty("checks")
                .GetProperty("postgresql")
                .GetProperty("status")
                .GetString());

        using var swaggerDocument = JsonDocument.Parse(await swaggerResponse.Content.ReadAsStreamAsync());
        Assert.Equal(
            "PulsePilot AI API",
            swaggerDocument.RootElement.GetProperty("info").GetProperty("title").GetString());
    }
}
