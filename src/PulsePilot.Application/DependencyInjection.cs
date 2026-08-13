using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application.Actions;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Backlog;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.FeedbackClusters;
using PulsePilot.Application.FeedbackProcessing;
using PulsePilot.Application.Prioritization;
using PulsePilot.Application.Tools;

namespace PulsePilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBacklogItemService, BacklogItemService>();
        services.AddScoped<IPendingActionService, PendingActionService>();
        services.AddScoped<IPendingActionRecommender, PendingActionRecommender>();
        services.AddScoped<ICreateBacklogItemTool, CreateBacklogItemTool>();
        services.AddScoped<IGetFeedbackStatisticsTool, GetFeedbackStatisticsTool>();
        services.AddScoped<ISearchSimilarFeedbackTool, SearchSimilarFeedbackTool>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IFeedbackClusterService, FeedbackClusterService>();
        services.AddScoped<IFeedbackAnalysisProcessor, FeedbackAnalysisProcessor>();
        services.AddSingleton<IPriorityScoreCalculator, PriorityScoreCalculator>();
        services.AddOptions<FeedbackProcessingOptions>();
        services.AddOptions<FeedbackStatisticsOptions>();
        services.AddOptions<SemanticSearchOptions>();
        services.AddOptions<PriorityScoringOptions>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
