using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.Feedback;
using PulsePilot.Application.Tools;

namespace PulsePilot.Application.Agents;

public sealed class AgentToolExecutor(
    ISearchSimilarFeedbackTool searchSimilarFeedbackTool,
    IGetFeedbackStatisticsTool feedbackStatisticsTool,
    IGetTrendingIssuesTool trendingIssuesTool,
    IGenerateReportTool reportTool,
    IOptions<SemanticSearchOptions> semanticSearchOptions,
    IOptions<FeedbackStatisticsOptions> feedbackStatisticsOptions,
    IOptions<TrendingIssuesOptions> trendingIssuesOptions,
    IOptions<ReportGenerationOptions> reportGenerationOptions) : IAgentToolExecutor
{
    private const int MaximumAgentListResultCount = 10;
    private const int MaximumFeedbackContentLength = 1_000;
    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    private readonly SemanticSearchOptions _searchOptions = semanticSearchOptions.Value;
    private readonly FeedbackStatisticsOptions _statisticsOptions =
        feedbackStatisticsOptions.Value;
    private readonly TrendingIssuesOptions _trendingOptions = trendingIssuesOptions.Value;
    private readonly ReportGenerationOptions _reportOptions = reportGenerationOptions.Value;

    public async Task<AgentToolExecutionOutput> ExecuteAsync(
        Guid workspaceId,
        AgentToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        ArgumentNullException.ThrowIfNull(toolCall);

        if (!IsKnownTool(toolCall.ToolName))
        {
            throw new InvalidOperationException(
                "The requested agent tool is not registered in the backend catalog.");
        }

        try
        {
            return toolCall.ToolName switch
            {
                AgentToolNames.SearchSimilarFeedback =>
                    await ExecuteSearchAsync(
                        workspaceId,
                        toolCall.ArgumentsJson,
                        cancellationToken),
                AgentToolNames.GetFeedbackStatistics =>
                    await ExecuteStatisticsAsync(
                        workspaceId,
                        toolCall.ArgumentsJson,
                        cancellationToken),
                AgentToolNames.GetTrendingIssues =>
                    await ExecuteTrendingIssuesAsync(
                        workspaceId,
                        toolCall.ArgumentsJson,
                        cancellationToken),
                AgentToolNames.GenerateReport =>
                    await ExecuteReportAsync(
                        workspaceId,
                        toolCall.ArgumentsJson,
                        cancellationToken),
                _ => throw new InvalidOperationException(
                    "The requested agent tool is not registered in the backend catalog."),
            };
        }
        catch (JsonException)
        {
            return CreateFailure(
                "invalid_arguments",
                "Tool arguments did not match the accepted contract.");
        }
        catch (NotFoundException)
        {
            return CreateFailure(
                "not_found",
                "The requested workspace resource was not found.");
        }
        catch (ConflictException)
        {
            return CreateFailure(
                "conflict",
                "The requested tool operation is not available in the current resource state.");
        }
        catch (ArgumentException)
        {
            return CreateFailure(
                "invalid_arguments",
                "Tool arguments did not match the accepted contract.");
        }
        catch (LlmProviderException)
        {
            return CreateFailure(
                "tool_unavailable",
                "The report generator is currently unavailable.");
        }
    }

    private async Task<AgentToolExecutionOutput> ExecuteSearchAsync(
        Guid workspaceId,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        var arguments = Deserialize<SearchSimilarFeedbackArguments>(
            argumentsJson,
            "feedbackId",
            "limit");
        var maximumLimit = Math.Min(
            _searchOptions.MaxLimit,
            MaximumAgentListResultCount);

        if (arguments.FeedbackId == Guid.Empty
            || !IsWithinOptionalRange(arguments.Limit, 1, maximumLimit))
        {
            throw new ArgumentException("Invalid search arguments.", nameof(argumentsJson));
        }

        var result = await searchSimilarFeedbackTool.ExecuteAsync(
            workspaceId,
            new SearchSimilarFeedbackToolInput(
                arguments.FeedbackId,
                arguments.Limit),
            cancellationToken);
        var safeResult = result with
        {
            Items = result.Items
                .Take(maximumLimit)
                .Select(item => item with
                {
                    Content = Truncate(item.Content, MaximumFeedbackContentLength),
                })
                .ToList(),
        };

        return CreateSuccess(safeResult);
    }

    private async Task<AgentToolExecutionOutput> ExecuteStatisticsAsync(
        Guid workspaceId,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        var arguments = Deserialize<GetFeedbackStatisticsArguments>(
            argumentsJson,
            "periodDays");

        if (!IsWithinOptionalRange(
                arguments.PeriodDays,
                1,
                _statisticsOptions.MaxPeriodDays))
        {
            throw new ArgumentException(
                "Invalid statistics arguments.",
                nameof(argumentsJson));
        }

        var result = await feedbackStatisticsTool.ExecuteAsync(
            workspaceId,
            new GetFeedbackStatisticsToolInput(arguments.PeriodDays),
            cancellationToken);

        return CreateSuccess(result);
    }

    private async Task<AgentToolExecutionOutput> ExecuteTrendingIssuesAsync(
        Guid workspaceId,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        var arguments = Deserialize<GetTrendingIssuesArguments>(
            argumentsJson,
            "periodDays",
            "limit");
        var maximumLimit = Math.Min(
            _trendingOptions.MaxLimit,
            MaximumAgentListResultCount);

        if (!IsWithinOptionalRange(
                arguments.PeriodDays,
                1,
                _trendingOptions.MaxPeriodDays)
            || !IsWithinOptionalRange(arguments.Limit, 1, maximumLimit))
        {
            throw new ArgumentException(
                "Invalid trending issue arguments.",
                nameof(argumentsJson));
        }

        var result = await trendingIssuesTool.ExecuteAsync(
            workspaceId,
            new GetTrendingIssuesToolInput(
                arguments.PeriodDays,
                arguments.Limit),
            cancellationToken);
        var safeResult = result with
        {
            Items = result.Items.Take(maximumLimit).ToList(),
        };

        return CreateSuccess(safeResult);
    }

    private async Task<AgentToolExecutionOutput> ExecuteReportAsync(
        Guid workspaceId,
        string argumentsJson,
        CancellationToken cancellationToken)
    {
        var arguments = Deserialize<GenerateReportArguments>(
            argumentsJson,
            "periodDays",
            "trendingIssueLimit");

        if (!IsWithinOptionalRange(
                arguments.PeriodDays,
                1,
                _reportOptions.MaxPeriodDays)
            || !IsWithinOptionalRange(
                arguments.TrendingIssueLimit,
                1,
                _reportOptions.MaxTrendingIssueLimit))
        {
            throw new ArgumentException("Invalid report arguments.", nameof(argumentsJson));
        }

        var result = await reportTool.ExecuteAsync(
            workspaceId,
            new GenerateReportToolInput(
                arguments.PeriodDays,
                arguments.TrendingIssueLimit),
            cancellationToken);

        return CreateSuccess(result);
    }

    private static T Deserialize<T>(
        string argumentsJson,
        params string[] requiredProperties)
        where T : class
    {
        using var document = JsonDocument.Parse(argumentsJson);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Tool arguments were not an object.");
        }

        var properties = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!properties.Add(property.Name))
            {
                throw new JsonException("Tool arguments contained a duplicate property.");
            }
        }

        if (requiredProperties.Any(property => !properties.Contains(property)))
        {
            throw new JsonException("Tool arguments omitted a required property.");
        }

        return JsonSerializer.Deserialize<T>(argumentsJson, SerializerOptions)
            ?? throw new JsonException("Tool arguments were null.");
    }

    private static bool IsKnownTool(string toolName)
    {
        return toolName is AgentToolNames.SearchSimilarFeedback
            or AgentToolNames.GetFeedbackStatistics
            or AgentToolNames.GetTrendingIssues
            or AgentToolNames.GenerateReport;
    }

    private static bool IsWithinOptionalRange(int? value, int minimum, int maximum)
    {
        return value is null || value >= minimum && value <= maximum;
    }

    private static string Truncate(string value, int maximumLength)
    {
        if (value.Length <= maximumLength)
        {
            return value;
        }

        var safeLength = char.IsHighSurrogate(value[maximumLength - 1])
            && char.IsLowSurrogate(value[maximumLength])
                ? maximumLength - 1
                : maximumLength;

        return value[..safeLength];
    }

    private static AgentToolExecutionOutput CreateSuccess<T>(T result)
    {
        return new AgentToolExecutionOutput(
            true,
            JsonSerializer.Serialize(result, SerializerOptions));
    }

    private static AgentToolExecutionOutput CreateFailure(string code, string message)
    {
        return new AgentToolExecutionOutput(
            false,
            JsonSerializer.Serialize(
                new ToolErrorEnvelope(new ToolError(code, message)),
                SerializerOptions));
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        serializerOptions.Converters.Add(new JsonStringEnumConverter(
            namingPolicy: null,
            allowIntegerValues: false));

        return serializerOptions;
    }

    private sealed record SearchSimilarFeedbackArguments(
        Guid FeedbackId,
        int? Limit);

    private sealed record GetFeedbackStatisticsArguments(int? PeriodDays);

    private sealed record GetTrendingIssuesArguments(
        int? PeriodDays,
        int? Limit);

    private sealed record GenerateReportArguments(
        int? PeriodDays,
        int? TrendingIssueLimit);

    private sealed record ToolErrorEnvelope(ToolError Error);

    private sealed record ToolError(string Code, string Message);
}
