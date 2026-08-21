using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using PulsePilot.Api.ErrorHandling;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Dashboard;
using PulsePilot.Domain.Common;
using PulsePilot.IntegrationTests.Persistence;

namespace PulsePilot.IntegrationTests.Api;

public sealed class GlobalExceptionHandlerTests(PostgreSqlFixture database)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task TryHandleAsync_NotFoundException_WritesNotFoundProblemDetails()
    {
        var problemDetailsService = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(
            problemDetailsService,
            NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "trace-not-found",
        };
        httpContext.Request.Path = "/workspaces/missing";

        var handled = await handler.TryHandleAsync(
            httpContext,
            new NotFoundException("Workspace", Guid.Empty),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.Equal("Resource not found", problemDetailsService.Written!.ProblemDetails.Title);
        Assert.Equal(
            "The requested resource could not be found.",
            problemDetailsService.Written.ProblemDetails.Detail);
        Assert.Equal(
            "not_found",
            problemDetailsService.Written.ProblemDetails.Extensions["code"]);
        Assert.Equal(
            "https://pulsepilot.ai/problems/not_found",
            problemDetailsService.Written.ProblemDetails.Type);
        Assert.Equal(
            "trace-not-found",
            problemDetailsService.Written.ProblemDetails.Extensions["traceId"]);
        Assert.Equal("no-store", httpContext.Response.Headers.CacheControl);
        Assert.Equal("nosniff", httpContext.Response.Headers.XContentTypeOptions);
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_DoesNotExposeExceptionMessage()
    {
        var problemDetailsService = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(
            problemDetailsService,
            NullLogger<GlobalExceptionHandler>.Instance);
        var httpContext = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException("sensitive database detail"),
            CancellationToken.None);

        var problemDetails = problemDetailsService.Written!.ProblemDetails;

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.DoesNotContain("sensitive", problemDetails.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("internal_error", problemDetails.Extensions["code"]);
    }

    [Fact]
    public async Task TryHandleAsync_KnownExceptions_UseSafePublicDetails()
    {
        var scenarios = new (Exception Exception, int Status, string Code)[]
        {
            (new InvalidCredentialsException(), 401, "invalid_credentials"),
            (new NotFoundException("secret resource identifier"), 404, "not_found"),
            (new ForbiddenException("secret authorization reason"), 403, "access_denied"),
            (new ConcurrencyConflictException(
                "secret database concurrency detail",
                new InvalidOperationException()), 409, "concurrency_conflict"),
            (new ConflictException("secret state detail"), 409, "conflict"),
            (new LlmProviderException(
                LlmProviderFailureKind.ProviderFailure,
                "secret provider payload"), 502, "ai_provider_unavailable"),
            (new DomainException("secret domain state"), 422, "business_rule_violation"),
            (new BadHttpRequestException("secret malformed body"), 400, "bad_request"),
        };

        foreach (var scenario in scenarios)
        {
            var problemDetailsService = new CapturingProblemDetailsService();
            var handler = new GlobalExceptionHandler(
                problemDetailsService,
                NullLogger<GlobalExceptionHandler>.Instance);
            var httpContext = new DefaultHttpContext();

            var handled = await handler.TryHandleAsync(
                httpContext,
                scenario.Exception,
                CancellationToken.None);
            var problemDetails = problemDetailsService.Written!.ProblemDetails;

            Assert.True(handled);
            Assert.Equal(scenario.Status, httpContext.Response.StatusCode);
            Assert.Equal(scenario.Code, problemDetails.Extensions["code"]);
            Assert.DoesNotContain(
                "secret",
                problemDetails.Detail,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_ReturnsBoundedValidationContract()
    {
        var problemDetailsService = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(
            problemDetailsService,
            NullLogger<GlobalExceptionHandler>.Instance);
        var exception = new ValidationException(
        [
            new ValidationFailure("Email", "Email is required."),
            new ValidationFailure("Email", "Email is required."),
            new ValidationFailure("Password", "Password is too short."),
        ]);

        await handler.TryHandleAsync(
            new DefaultHttpContext(),
            exception,
            CancellationToken.None);

        var problemDetails = Assert.IsType<HttpValidationProblemDetails>(
            problemDetailsService.Written!.ProblemDetails);
        Assert.Equal("validation_error", problemDetails.Extensions["code"]);
        Assert.Single(problemDetails.Errors["Email"]);
        Assert.Single(problemDetails.Errors["Password"]);
    }

    [Fact]
    public async Task MalformedJson_ReturnsSafeSerializedProblemDetails()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        const string sensitiveEmail = "private-customer@example.com";
        const string sensitivePassword = "private-password-value";
        using var content = new StringContent(
            $"{{\"email\":\"{sensitiveEmail}\",\"password\":\"{sensitivePassword}\"",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/auth/login", content);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "validation_error",
            document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
        Assert.DoesNotContain(sensitiveEmail, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(sensitivePassword, body, StringComparison.Ordinal);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var values));
        Assert.Contains("nosniff", values);
    }

    [Fact]
    public async Task UnexpectedEndpointException_ReturnsSafeSerializedProblemDetails()
    {
        await using var factory = new PulsePilotApiFactory(
            database.ConnectionString,
            services =>
            {
                services.RemoveAll<IDashboardService>();
                services.AddSingleton<IDashboardService, ThrowingDashboardService>();
            });
        using var client = factory.CreateClient();
        var email = $"error-owner-{Guid.CreateVersion7():N}@example.com";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterCommand(
                email,
                "Error Contract Owner",
                "correct-horse-battery-staple",
                "Error Contract Workspace"));
        var authentication = await registerResponse.Content
            .ReadFromJsonAsync<AuthenticationResponse>();

        Assert.NotNull(authentication);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication.AccessToken);

        using var response = await client.GetAsync("/api/dashboard/summary?periodDays=7");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("internal_error", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "The server could not complete the request.",
            document.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain(
            ThrowingDashboardService.SensitiveMessage,
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(document.RootElement.TryGetProperty("exception", out _));
        Assert.False(document.RootElement.TryGetProperty("stackTrace", out _));
    }

    [Fact]
    public async Task UnknownApiRoute_ReturnsStandardProblemDetails()
    {
        await using var factory = new PulsePilotApiFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var email = $"missing-route-{Guid.CreateVersion7():N}@example.com";
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterCommand(
                email,
                "Missing Route Owner",
                "correct-horse-battery-staple",
                "Missing Route Workspace"));
        var authentication = await registerResponse.Content
            .ReadFromJsonAsync<AuthenticationResponse>();

        Assert.NotNull(authentication);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            authentication.AccessToken);

        using var response = await client.GetAsync("/api/does-not-exist?token=secret");
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("not_found", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "https://pulsepilot.ai/problems/not_found",
            document.RootElement.GetProperty("type").GetString());
        Assert.Equal(
            "/api/does-not-exist",
            document.RootElement.GetProperty("instance").GetString());
        Assert.DoesNotContain(
            "token=secret",
            document.RootElement.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
    }

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Written { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Written = context;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class ThrowingDashboardService : IDashboardService
    {
        public const string SensitiveMessage = "database password was exposed internally";

        public Task<DashboardSummaryResponse> GetSummaryAsync(
            DashboardSummaryQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(SensitiveMessage);
        }

        public Task<DashboardTrendingResponse> GetTrendingAsync(
            DashboardTrendingQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(SensitiveMessage);
        }
    }
}
