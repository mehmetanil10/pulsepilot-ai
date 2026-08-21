using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PulsePilot.Api.ErrorHandling;
using PulsePilot.Application.Authentication;

namespace PulsePilot.Api.Protection;

public static class ApiProtectionServiceCollectionExtensions
{
    public static IServiceCollection AddApiProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(ApiProtectionOptions.SectionName);
        services.AddOptions<ApiProtectionOptions>()
            .Bind(section)
            .Validate(
                options => options.GeneralPermitLimit is >= 1 and <= 100_000,
                "General API permit limit must be between 1 and 100000.")
            .Validate(
                options => options.AuthenticationPermitLimit is >= 1 and <= 10_000,
                "Authentication permit limit must be between 1 and 10000.")
            .Validate(
                options => options.AiPermitLimit is >= 1 and <= 10_000,
                "AI permit limit must be between 1 and 10000.")
            .Validate(
                options => options.WindowSeconds is >= 1 and <= 3_600,
                "API rate-limit window must be between 1 and 3600 seconds.")
            .Validate(
                options => options.QueueLimit is >= 0 and <= 1_000,
                "API rate-limit queue must be between 0 and 1000 requests.")
            .Validate(
                options => options.MaxRequestBodyBytes is >= 1_024 and <= 10_485_760,
                "Maximum request body size must be between 1024 bytes and 10 MiB.")
            .ValidateOnStart();

        services.AddOptions<KestrelServerOptions>()
            .Configure<IOptions<ApiProtectionOptions>>((server, configured) =>
                server.Limits.MaxRequestBodySize = configured.Value.MaxRequestBodyBytes);
        services.AddOptions<IISServerOptions>()
            .Configure<IOptions<ApiProtectionOptions>>((server, configured) =>
                server.MaxRequestBodySize = configured.Value.MaxRequestBodyBytes);

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<ApiProtectionOptions>>((options, configured) =>
                ConfigureRateLimiter(options, configured.Value));

        return services;
    }

    private static void ConfigureRateLimiter(
        RateLimiterOptions options,
        ApiProtectionOptions protection)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = WriteRateLimitProblemAsync;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            if (!protection.RateLimitingEnabled
                || !context.Request.Path.StartsWithSegments("/api"))
            {
                return RateLimitPartition.GetNoLimiter("unlimited");
            }

            return RateLimitPartition.GetFixedWindowLimiter(
                GetAuthenticatedOrIpPartition(context),
                _ => CreateFixedWindowOptions(protection.GeneralPermitLimit, protection));
        });
        options.AddPolicy(ApiRateLimitPolicies.Authentication, context =>
            protection.RateLimitingEnabled
                ? RateLimitPartition.GetFixedWindowLimiter(
                    GetIpPartition(context),
                    _ => CreateFixedWindowOptions(
                        protection.AuthenticationPermitLimit,
                        protection))
                : RateLimitPartition.GetNoLimiter("authentication-unlimited"));
        options.AddPolicy(ApiRateLimitPolicies.Ai, context =>
            protection.RateLimitingEnabled
                ? RateLimitPartition.GetFixedWindowLimiter(
                    GetAuthenticatedOrIpPartition(context),
                    _ => CreateFixedWindowOptions(protection.AiPermitLimit, protection))
                : RateLimitPartition.GetNoLimiter("ai-unlimited"));
    }

    private static FixedWindowRateLimiterOptions CreateFixedWindowOptions(
        int permitLimit,
        ApiProtectionOptions protection)
    {
        return new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(protection.WindowSeconds),
            QueueLimit = protection.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        };
    }

    private static string GetAuthenticatedOrIpPartition(HttpContext context)
    {
        var workspaceId = context.User.FindFirst(TokenClaimNames.WorkspaceId)?.Value;
        var userId = context.User.FindFirst(TokenClaimNames.Subject)?.Value;

        return !string.IsNullOrWhiteSpace(workspaceId) && !string.IsNullOrWhiteSpace(userId)
            ? $"workspace:{workspaceId}:user:{userId}"
            : GetIpPartition(context);
    }

    private static string GetIpPartition(HttpContext context)
    {
        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    private static async ValueTask WriteRateLimitProblemAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var retryAfter = context.Lease.TryGetMetadata(
            MetadataName.RetryAfter,
            out var retryAfterValue)
            ? retryAfterValue
            : TimeSpan.FromSeconds(1);
        context.HttpContext.Response.Headers.RetryAfter = Math.Max(
                1,
                (int)Math.Ceiling(retryAfter.TotalSeconds))
            .ToString(CultureInfo.InvariantCulture);

        var problem = ApiProblemDetailsFactory.Create(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "Too many requests",
            "The request rate limit has been exceeded. Retry after the indicated delay.",
            "rate_limit_exceeded");
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var problemDetailsService = context.HttpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = problem,
        });
    }
}
