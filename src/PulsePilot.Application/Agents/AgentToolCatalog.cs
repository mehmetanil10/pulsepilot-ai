using System.Text.Json;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.Tools;

namespace PulsePilot.Application.Agents;

internal sealed class AgentToolCatalog : IAgentToolCatalog
{
    private const int MaximumAgentListResultCount = 10;
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyList<AgentToolDefinition> _tools;

    public AgentToolCatalog(
        IOptions<SemanticSearchOptions> semanticSearchOptions,
        IOptions<FeedbackStatisticsOptions> feedbackStatisticsOptions,
        IOptions<TrendingIssuesOptions> trendingIssuesOptions,
        IOptions<ReportGenerationOptions> reportGenerationOptions)
    {
        var searchOptions = semanticSearchOptions.Value;
        var statisticsOptions = feedbackStatisticsOptions.Value;
        var trendOptions = trendingIssuesOptions.Value;
        var reportOptions = reportGenerationOptions.Value;
        var searchLimit = Math.Min(
            searchOptions.MaxLimit,
            MaximumAgentListResultCount);
        var trendLimit = Math.Min(
            trendOptions.MaxLimit,
            MaximumAgentListResultCount);

        _tools =
        [
            new AgentToolDefinition(
                AgentToolNames.SearchSimilarFeedback,
                $"Finds at most {searchLimit} semantically similar feedback records in the current workspace. Input: feedbackId (UUID) and nullable limit (1-{searchLimit}; null uses the configured default). Returns feedbackId, similarityThreshold, count, and items with feedbackId, feedbackClusterId, title, truncated untrusted content, source, similarity, and createdAt. A failure returns an error object with code and safe message.",
                CreateSchema(
                    new Dictionary<string, object>
                    {
                        ["feedbackId"] = new
                        {
                            type = "string",
                            description = "The UUID of the source feedback record.",
                        },
                        ["limit"] = CreateNullableBoundedIntegerSchema(
                            1,
                            searchLimit,
                            "Maximum number of matches; null uses the configured default."),
                    },
                    ["feedbackId", "limit"])),
            new AgentToolDefinition(
                AgentToolNames.GetFeedbackStatistics,
                $"Returns aggregate feedback statistics for the current workspace. Input: nullable periodDays (1-{statisticsOptions.MaxPeriodDays}; null uses {statisticsOptions.DefaultPeriodDays}). Returns the UTC time window, total/analyzed counts, average severity, and complete processing-status, source, category, component, sentiment, and severity breakdowns. A failure returns an error object with code and safe message.",
                CreateSchema(
                    new Dictionary<string, object>
                    {
                        ["periodDays"] = CreateNullableBoundedIntegerSchema(
                            1,
                            statisticsOptions.MaxPeriodDays,
                            "Lookback period in days; null uses the configured default."),
                    },
                    ["periodDays"])),
            new AgentToolDefinition(
                AgentToolNames.GetTrendingIssues,
                $"Compares the current and preceding equal feedback windows in the current workspace. Inputs: nullable periodDays (1-{trendOptions.MaxPeriodDays}; null uses {trendOptions.DefaultPeriodDays}) and nullable limit (1-{trendLimit}; null uses the configured default). Returns both UTC windows and growing issue clusters with counts, delta, growth percentage, priority, and isNew. A failure returns an error object with code and safe message.",
                CreateSchema(
                    new Dictionary<string, object>
                    {
                        ["periodDays"] = CreateNullableBoundedIntegerSchema(
                            1,
                            trendOptions.MaxPeriodDays,
                            "Size of each comparison window in days; null uses the configured default."),
                        ["limit"] = CreateNullableBoundedIntegerSchema(
                            1,
                            trendLimit,
                            "Maximum number of growing issues; null uses the configured default."),
                    },
                    ["periodDays", "limit"])),
            new AgentToolDefinition(
                AgentToolNames.GenerateReport,
                $"Generates a grounded product intelligence report for the current workspace from aggregate statistics and trends. Inputs: nullable periodDays (1-{reportOptions.MaxPeriodDays}; null uses {reportOptions.DefaultPeriodDays}) and nullable trendingIssueLimit (1-{reportOptions.MaxTrendingIssueLimit}; null uses {reportOptions.DefaultTrendingIssueLimit}). Returns generatedAt, source statistics, source trendingIssues, and a validated report with title, executiveSummary, keyInsights, and recommendedEngineeringPriorities. A failure returns an error object with code and safe message.",
                CreateSchema(
                    new Dictionary<string, object>
                    {
                        ["periodDays"] = CreateNullableBoundedIntegerSchema(
                            1,
                            reportOptions.MaxPeriodDays,
                            "Report lookback period in days; null uses the configured default."),
                        ["trendingIssueLimit"] = CreateNullableBoundedIntegerSchema(
                            1,
                            reportOptions.MaxTrendingIssueLimit,
                            "Maximum number of trends included; null uses the configured default."),
                    },
                    ["periodDays", "trendingIssueLimit"])),
        ];
    }

    public IReadOnlyList<AgentToolDefinition> ListTools() => _tools;

    private static object CreateNullableBoundedIntegerSchema(
        int minimum,
        int maximum,
        string description)
    {
        return new
        {
            type = new[] { "integer", "null" },
            minimum,
            maximum,
            description,
        };
    }

    private static string CreateSchema(
        IReadOnlyDictionary<string, object> properties,
        IReadOnlyList<string> required)
    {
        return JsonSerializer.Serialize(
            new
            {
                type = "object",
                properties,
                required,
                additionalProperties = false,
            },
            SerializerOptions);
    }
}
