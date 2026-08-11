using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Authentication;

namespace PulsePilot.Api.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    private const int MinimumTokenLifetimeMinutes = 5;
    private const int MaximumTokenLifetimeMinutes = 1_440;

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer),
                "JWT issuer is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Audience),
                "JWT audience is required.")
            .Validate(
                options => Encoding.UTF8.GetByteCount(options.Secret) >=
                    JwtOptions.MinimumSecretSizeInBytes,
                $"JWT secret must be at least {JwtOptions.MinimumSecretSizeInBytes} bytes.")
            .Validate(
                options => options.ExpirationMinutes is >= MinimumTokenLifetimeMinutes
                    and <= MaximumTokenLifetimeMinutes,
                $"JWT expiration must be between {MinimumTokenLifetimeMinutes} and " +
                $"{MaximumTokenLifetimeMinutes} minutes.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>(ConfigureJwtBearer);

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(TokenClaimNames.Subject)
                .RequireClaim(TokenClaimNames.WorkspaceId)
                .RequireClaim(TokenClaimNames.Role, "Admin", "Member")
                .Build());

        return services;
    }

    private static void ConfigureJwtBearer(
        JwtBearerOptions bearerOptions,
        IOptions<JwtOptions> jwtOptionsAccessor)
    {
        var jwtOptions = jwtOptionsAccessor.Value;

        bearerOptions.MapInboundClaims = false;
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = TokenClaimNames.Name,
            RoleClaimType = TokenClaimNames.Role,
        };
        bearerOptions.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await WriteProblemDetailsAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Authentication required",
                    "A valid bearer token is required.");
            },
            OnForbidden = context => WriteProblemDetailsAsync(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "Access denied",
                "The authenticated user is not allowed to access this resource."),
        };
    }

    private static Task WriteProblemDetailsAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail)
    {
        if (httpContext.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        httpContext.Response.StatusCode = statusCode;
        var problemDetailsService = httpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();

        return problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
            },
        }).AsTask();
    }
}
