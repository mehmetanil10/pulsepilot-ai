using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Authentication;
using PulsePilot.IntegrationTests.Persistence;

namespace PulsePilot.IntegrationTests.Api;

public sealed class ApiProtectionTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task ApiAndHealthResponses_IncludeSecurityHeadersWithoutBreakingSwagger()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        using var apiResponse = await client.GetAsync("/api/feedback");
        using var healthResponse = await client.GetAsync("/health/live");
        using var swaggerResponse = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.Unauthorized, apiResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        AssertSecurityHeaders(apiResponse);
        AssertSecurityHeaders(healthResponse);
        Assert.False(swaggerResponse.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task AuthenticationPolicy_ReturnsSafeProblemDetailsAndRetryAfter()
    {
        var configuration = new Dictionary<string, string?>
        {
            ["ApiProtection:GeneralPermitLimit"] = "100",
            ["ApiProtection:AuthenticationPermitLimit"] = "2",
            ["ApiProtection:WindowSeconds"] = "60",
        };
        await using var factory = new PulsePilotApiFactory(
            database.ConnectionString,
            configurationOverrides: configuration);
        using var client = factory.CreateClient();
        var command = new LoginCommand(
            $"rate-limit-{Guid.CreateVersion7():N}@example.com",
            "invalid-password");

        using var first = await client.PostAsJsonAsync("/api/auth/login", command);
        using var second = await client.PostAsJsonAsync("/api/auth/login", command);
        using var rejected = await client.PostAsJsonAsync("/api/auth/login", command);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.True(rejected.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        AssertSecurityHeaders(rejected);

        using var document = JsonDocument.Parse(await rejected.Content.ReadAsStreamAsync());
        Assert.Equal(429, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "rate_limit_exceeded",
            document.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(
            command.Email,
            document.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthChecks_BypassApiRateLimitAndBodyLimitIsConfigured()
    {
        var configuration = new Dictionary<string, string?>
        {
            ["ApiProtection:GeneralPermitLimit"] = "1",
            ["ApiProtection:MaxRequestBodyBytes"] = "2048",
        };
        await using var factory = new PulsePilotApiFactory(
            database.ConnectionString,
            configurationOverrides: configuration);
        using var client = factory.CreateClient();

        using var first = await client.GetAsync("/health/live");
        using var second = await client.GetAsync("/health/live");
        using var third = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        Assert.Equal(
            2048,
            factory.Services
                .GetRequiredService<IOptions<KestrelServerOptions>>()
                .Value
                .Limits
                .MaxRequestBodySize);
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        AssertHeader(response, "Content-Security-Policy", "default-src 'none'");
        AssertHeader(response, "Permissions-Policy", "camera=()");
        AssertHeader(response, "Referrer-Policy", "no-referrer");
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "DENY");
    }

    private static void AssertHeader(
        HttpResponseMessage response,
        string name,
        string expectedValue)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values));
        Assert.Contains(values, value => value.Contains(expectedValue, StringComparison.Ordinal));
    }
}
