#pragma warning disable OPENAI001

using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;
using OpenAI.Responses;
using Pgvector.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.Prioritization;
using PulsePilot.Application.Tools;
using PulsePilot.Infrastructure.AI;
using PulsePilot.Infrastructure.Authentication;
using PulsePilot.Infrastructure.Persistence;
using PulsePilot.Infrastructure.Persistence.Repositories;
using PulsePilot.Infrastructure.Persistence.Seeding;

namespace PulsePilot.Infrastructure;

public static class DependencyInjection
{
    private const string DatabaseConnectionStringName = "Database";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var runtimeConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = runtimeConfiguration.GetConnectionString(
                DatabaseConnectionStringName);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{DatabaseConnectionStringName}' is required.");
            }

            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.UseVector();
                    npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
        });

        services.Configure<DemoSeedOptions>(
            configuration.GetSection(DemoSeedOptions.SectionName));

        services.AddOptions<SemanticSearchOptions>()
            .Bind(configuration.GetSection(SemanticSearchOptions.SectionName))
            .Validate(
                options => options.SimilarityThreshold is >= 0 and <= 1,
                "Semantic search similarity threshold must be between 0 and 1.")
            .Validate(
                options => options.DefaultLimit is >= 1
                    and <= SemanticSearchOptions.MaximumResultLimit,
                $"Semantic search default limit must be between 1 and {SemanticSearchOptions.MaximumResultLimit}.")
            .Validate(
                options => options.MaxLimit is >= 1
                    and <= SemanticSearchOptions.MaximumResultLimit,
                $"Semantic search maximum limit must be between 1 and {SemanticSearchOptions.MaximumResultLimit}.")
            .Validate(
                options => options.DefaultLimit <= options.MaxLimit,
                "Semantic search default limit cannot exceed its maximum limit.")
            .ValidateOnStart();

        services.AddOptions<FeedbackStatisticsOptions>()
            .Bind(configuration.GetSection(FeedbackStatisticsOptions.SectionName))
            .Validate(
                options => options.DefaultPeriodDays is >= 1
                    and <= FeedbackStatisticsOptions.MaximumAllowedPeriod,
                $"Feedback statistics default period must be between 1 and {FeedbackStatisticsOptions.MaximumAllowedPeriod} days.")
            .Validate(
                options => options.MaxPeriodDays is >= 1
                    and <= FeedbackStatisticsOptions.MaximumAllowedPeriod,
                $"Feedback statistics maximum period must be between 1 and {FeedbackStatisticsOptions.MaximumAllowedPeriod} days.")
            .Validate(
                options => options.DefaultPeriodDays <= options.MaxPeriodDays,
                "Feedback statistics default period cannot exceed its maximum period.")
            .ValidateOnStart();

        services.AddOptions<TrendingIssuesOptions>()
            .Bind(configuration.GetSection(TrendingIssuesOptions.SectionName))
            .Validate(
                options => options.DefaultPeriodDays is >= 1
                    and <= TrendingIssuesOptions.MaximumAllowedPeriod,
                $"Trending issues default period must be between 1 and {TrendingIssuesOptions.MaximumAllowedPeriod} days.")
            .Validate(
                options => options.MaxPeriodDays is >= 1
                    and <= TrendingIssuesOptions.MaximumAllowedPeriod,
                $"Trending issues maximum period must be between 1 and {TrendingIssuesOptions.MaximumAllowedPeriod} days.")
            .Validate(
                options => options.DefaultPeriodDays <= options.MaxPeriodDays,
                "Trending issues default period cannot exceed its maximum period.")
            .Validate(
                options => options.DefaultLimit is >= 1
                    and <= TrendingIssuesOptions.MaximumResultLimit,
                $"Trending issues default limit must be between 1 and {TrendingIssuesOptions.MaximumResultLimit}.")
            .Validate(
                options => options.MaxLimit is >= 1
                    and <= TrendingIssuesOptions.MaximumResultLimit,
                $"Trending issues maximum limit must be between 1 and {TrendingIssuesOptions.MaximumResultLimit}.")
            .Validate(
                options => options.DefaultLimit <= options.MaxLimit,
                "Trending issues default limit cannot exceed its maximum limit.")
            .ValidateOnStart();

        services.AddOptions<PriorityScoringOptions>()
            .Bind(configuration.GetSection(PriorityScoringOptions.SectionName))
            .Validate(
                options => options.SeverityWeight is >= 0 and <= 1
                    && options.FrequencyWeight is >= 0 and <= 1
                    && options.CustomerImpactWeight is >= 0 and <= 1
                    && options.RecencyWeight is >= 0 and <= 1,
                "Priority scoring weights must each be between 0 and 1.")
            .Validate(
                options => options.SeverityWeight
                    + options.FrequencyWeight
                    + options.CustomerImpactWeight
                    + options.RecencyWeight == 1m,
                "Priority scoring weights must total 1.")
            .Validate(
                options => options.FrequencyNormalizationCount is >= 1 and <= 100_000,
                "Priority frequency normalization count must be between 1 and 100000.")
            .Validate(
                options => options.CustomerImpactNormalizationCount is >= 1 and <= 100_000,
                "Priority customer impact normalization count must be between 1 and 100000.")
            .Validate(
                options => options.RecencyWindowDays is >= 1 and <= 365,
                "Priority recency window must be between 1 and 365 days.")
            .Validate(
                options => options.P1Threshold is > 0 and <= 100
                    && options.P2Threshold is > 0
                    && options.P3Threshold is >= 0
                    && options.P1Threshold > options.P2Threshold
                    && options.P2Threshold > options.P3Threshold,
                "Priority thresholds must be ordered P1 > P2 > P3 within 0 to 100.")
            .ValidateOnStart();

        services.AddOptions<OpenAIOptions>()
            .Bind(configuration.GetSection(OpenAIOptions.SectionName))
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ApiKey),
                "OpenAI API key is required when the provider is enabled.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Model),
                "OpenAI model is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.EmbeddingModel),
                "OpenAI embedding model is required.")
            .Validate(
                options => options.EmbeddingDimensions == PulsePilot.Domain.Feedback.FeedbackEmbedding.Dimensions,
                $"OpenAI embedding dimensions must equal {PulsePilot.Domain.Feedback.FeedbackEmbedding.Dimensions}.")
            .Validate(
                options => options.Endpoint is { IsAbsoluteUri: true }
                    && options.Endpoint.Scheme == Uri.UriSchemeHttps,
                "OpenAI endpoint must be an absolute HTTPS URI.")
            .Validate(
                options => options.MaxOutputTokenCount is >= 128 and <= 16_384,
                "OpenAI max output token count must be between 128 and 16384.")
            .Validate(
                options => options.NetworkTimeoutSeconds is >= 1 and <= 300,
                "OpenAI network timeout must be between 1 and 300 seconds.")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            var openAIOptions = serviceProvider
                .GetRequiredService<IOptions<OpenAIOptions>>()
                .Value;
            var apiKey = openAIOptions.Enabled
                ? openAIOptions.ApiKey
                : "openai-provider-disabled";
            var clientOptions = new ResponsesClientOptions
            {
                Endpoint = openAIOptions.Endpoint,
                NetworkTimeout = TimeSpan.FromSeconds(openAIOptions.NetworkTimeoutSeconds),
                RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
                ClientLoggingOptions = new ClientLoggingOptions
                {
                    EnableLogging = false,
                    EnableMessageLogging = false,
                    EnableMessageContentLogging = false,
                },
            };

            return new ResponsesClient(new ApiKeyCredential(apiKey), clientOptions);
        });

        services.AddSingleton(serviceProvider =>
        {
            var openAIOptions = serviceProvider
                .GetRequiredService<IOptions<OpenAIOptions>>()
                .Value;
            var apiKey = openAIOptions.Enabled
                ? openAIOptions.ApiKey
                : "openai-provider-disabled";
            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = openAIOptions.Endpoint,
                NetworkTimeout = TimeSpan.FromSeconds(openAIOptions.NetworkTimeoutSeconds),
                RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
                ClientLoggingOptions = new ClientLoggingOptions
                {
                    EnableLogging = false,
                    EnableMessageLogging = false,
                    EnableMessageContentLogging = false,
                },
            };

            return new EmbeddingClient(
                openAIOptions.EmbeddingModel,
                new ApiKeyCredential(apiKey),
                clientOptions);
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IWorkspaceMemberRepository, WorkspaceMemberRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();
        services.AddScoped<IFeedbackStatisticsRepository, FeedbackStatisticsRepository>();
        services.AddScoped<ITrendingIssueRepository, TrendingIssueRepository>();
        services.AddScoped<IFeedbackAnalysisRepository, FeedbackAnalysisRepository>();
        services.AddScoped<IFeedbackEmbeddingRepository, FeedbackEmbeddingRepository>();
        services.AddScoped<IFeedbackClusterRepository, FeedbackClusterRepository>();
        services.AddScoped<IPendingActionRepository, PendingActionRepository>();
        services.AddScoped<IBacklogItemRepository, BacklogItemRepository>();
        services.AddScoped<IPendingActionExecutionLock, PostgreSqlPendingActionExecutionLock>();
        services.AddScoped<IFeedbackClusterAssignmentLock, PostgreSqlFeedbackClusterAssignmentLock>();
        services.AddScoped<IFeedbackProcessingQueue, FeedbackProcessingQueue>();
        services.AddScoped<IDemoDataSeeder, DemoDataSeeder>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<IPasswordHasher, AspNetCorePasswordHasher>();
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddScoped<ILLMClient, OpenAILlmClient>();

        return services;
    }
}

#pragma warning restore OPENAI001
