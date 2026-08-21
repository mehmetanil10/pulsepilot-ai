using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PulsePilot.Api.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Authentication;
using PulsePilot.IntegrationTests.Persistence;

namespace PulsePilot.IntegrationTests.Api;

public sealed class AuthenticationEndpointTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task RegisterAndLogin_IssueUsableTokensAndStorePasswordHash()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var password = "correct-horse-battery-staple";
        var email = $"owner-{Guid.CreateVersion7():N}@example.com";
        var registerCommand = new RegisterCommand(
            email,
            "Workspace Owner",
            password,
            "Authentication Test Workspace");

        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            registerCommand);
        var registration = await registerResponse.Content
            .ReadFromJsonAsync<AuthenticationResponse>();

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.NotNull(registration);
        Assert.Equal("Bearer", registration.TokenType);
        Assert.Equal("Admin", registration.Role);
        Assert.False(string.IsNullOrWhiteSpace(registration.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            registration.AccessToken);
        using var currentUserResponse = await client.GetAsync("/api/auth/me");
        var currentUser = await currentUserResponse.Content
            .ReadFromJsonAsync<CurrentUserResponse>();

        Assert.Equal(HttpStatusCode.OK, currentUserResponse.StatusCode);
        Assert.NotNull(currentUser);
        Assert.Equal(registration.UserId, currentUser.UserId);
        Assert.Equal(registration.WorkspaceId, currentUser.WorkspaceId);
        Assert.Equal("Admin", currentUser.Role);

        client.DefaultRequestHeaders.Authorization = null;
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginCommand(email.ToUpperInvariant(), password));
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthenticationResponse>();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(login);
        Assert.Equal(registration.UserId, login.UserId);
        Assert.Equal(registration.WorkspaceId, login.WorkspaceId);
        Assert.NotEqual(registration.AccessToken, login.AccessToken);

        await using var serviceProvider = database.CreateServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var persistedUser = await scope.ServiceProvider
            .GetRequiredService<IUserRepository>()
            .GetByNormalizedEmailAsync(email.ToUpperInvariant());

        Assert.NotNull(persistedUser);
        Assert.NotEqual(password, persistedUser.PasswordHash);
        Assert.DoesNotContain(password, persistedUser.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_DuplicateNormalizedEmail_ReturnsConflictProblemDetails()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var email = $"duplicate-{Guid.CreateVersion7():N}@example.com";
        var command = new RegisterCommand(
            email,
            "First Owner",
            "correct-horse-battery-staple",
            "First Workspace");

        using var firstResponse = await client.PostAsJsonAsync("/api/auth/register", command);
        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            command with { Email = email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);

        using var document = JsonDocument.Parse(
            await duplicateResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Conflict", document.RootElement.GetProperty("title").GetString());
        Assert.Equal("conflict", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "The request conflicts with the current resource state.",
            document.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain(
            email,
            document.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsGenericUnauthorizedProblemDetails()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var unknownEmail = $"unknown-{Guid.CreateVersion7():N}@example.com";

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginCommand(unknownEmail, "incorrect-password"));
        using var currentUserResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, currentUserResponse.StatusCode);

        using var loginDocument = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStreamAsync());
        Assert.Equal(
            "Authentication failed",
            loginDocument.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "invalid_credentials",
            loginDocument.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain(
            unknownEmail,
            loginDocument.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);

        using var challengeDocument = JsonDocument.Parse(
            await currentUserResponse.Content.ReadAsStreamAsync());
        Assert.Equal(
            "Authentication required",
            challengeDocument.RootElement.GetProperty("title").GetString());
        Assert.Equal(
            "authentication_required",
            challengeDocument.RootElement.GetProperty("code").GetString());
    }
}
