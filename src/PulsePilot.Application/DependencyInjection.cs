using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Actions;
using PulsePilot.Application.Agents;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Backlog;
using PulsePilot.Application.Copilot;
using PulsePilot.Application.CustomerResponses;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.FeedbackClusters;
using PulsePilot.Application.FeedbackProcessing;
using PulsePilot.Application.Prioritization;
using PulsePilot.Application.Reports;
using PulsePilot.Application.Tools;

namespace PulsePilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining(typeof(DependencyInjection));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();
        services.AddScoped<IBacklogItemService, BacklogItemService>();
        services.AddScoped<ICustomerResponseDraftService, CustomerResponseDraftService>();
        services.AddScoped<ICopilotChatService, CopilotChatService>();
        services.AddScoped<IWeeklyReportService, WeeklyReportService>();
        services.AddScoped<IPendingActionService, PendingActionService>();
        services.AddScoped<IPendingActionRecommender, PendingActionRecommender>();
        services.AddScoped<ICreateBacklogItemTool, CreateBacklogItemTool>();
        services.AddScoped<IDraftCustomerResponseTool, DraftCustomerResponseTool>();
        services.AddScoped<IGenerateReportTool, GenerateReportTool>();
        services.AddScoped<IGetFeedbackStatisticsTool, GetFeedbackStatisticsTool>();
        services.AddScoped<IGetTrendingIssuesTool, GetTrendingIssuesTool>();
        services.AddScoped<ISearchSimilarFeedbackTool, SearchSimilarFeedbackTool>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IFeedbackClusterService, FeedbackClusterService>();
        services.AddScoped<IFeedbackAnalysisProcessor, FeedbackAnalysisProcessor>();
        services.AddSingleton<IPriorityScoreCalculator, PriorityScoreCalculator>();
        services.AddSingleton<IAgentToolCatalog, AgentToolCatalog>();
        services.TryAddScoped<IAgentTurnClient, UnavailableAgentTurnClient>();
        services.TryAddScoped<IAgentToolExecutor, DisabledAgentToolExecutor>();
        services.AddOptions<AgentOrchestrationOptions>();
        services.AddOptions<FeedbackProcessingOptions>();
        services.AddOptions<CustomerResponseDraftingOptions>();
        services.AddOptions<ReportGenerationOptions>();
        services.AddOptions<FeedbackStatisticsOptions>();
        services.AddOptions<SemanticSearchOptions>();
        services.AddOptions<TrendingIssuesOptions>();
        services.AddOptions<PriorityScoringOptions>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
