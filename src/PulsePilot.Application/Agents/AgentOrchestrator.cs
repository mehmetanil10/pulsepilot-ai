using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.Application.Agents;

internal sealed partial class AgentOrchestrator(
    IAgentTurnClient agentTurnClient,
    IAgentToolCatalog agentToolCatalog,
    IAgentToolExecutor agentToolExecutor,
    IOptions<AgentOrchestrationOptions> options) : IAgentOrchestrator
{
    private const int MaxToolNameLength = 64;
    private const int MaxToolDescriptionLength = 1_000;
    private const int MaxToolSchemaLength = 16_000;
    private const int MaxCallIdLength = 128;
    private const int MaxContinuationItemLength = 100_000;
    private const int MaxContinuationItems = 64;
    private const int MaxTotalContinuationLength = 250_000;

    private readonly AgentOrchestrationOptions _options = options.Value;

    public async Task<AgentOrchestrationResult> RunAsync(
        Guid workspaceId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        if (string.IsNullOrWhiteSpace(userMessage)
            || userMessage.Length > _options.MaxUserMessageLength)
        {
            throw new ArgumentException(
                $"User message must contain between 1 and {_options.MaxUserMessageLength} characters.",
                nameof(userMessage));
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.ExecutionTimeoutSeconds));

        try
        {
            return await RunCoreAsync(
                workspaceId,
                userMessage.Trim(),
                timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmProviderException(
                LlmProviderFailureKind.ProviderUnavailable,
                "Agent orchestration timed out.",
                isTransient: true);
        }
    }

    private async Task<AgentOrchestrationResult> RunCoreAsync(
        Guid workspaceId,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var availableTools = agentToolCatalog.ListTools()
            ?? throw new InvalidOperationException(
                "Agent tool catalog returned no collection.");
        var tools = availableTools.ToArray();
        var allowedToolNames = ValidateToolCatalog(tools);
        var exchanges = new List<AgentToolExchange>();
        var continuationItems = new List<AgentContinuationItem>();
        var usages = new List<AgentToolUsage>();
        var usedCallIds = new HashSet<string>(StringComparer.Ordinal);

        for (var turnNumber = 1; turnNumber <= _options.MaxTurns; turnNumber++)
        {
            var turn = await agentTurnClient.CreateTurnAsync(
                new AgentTurnRequest(
                    userMessage,
                    tools,
                    exchanges.ToArray(),
                    continuationItems.ToArray()),
                cancellationToken);
            ValidateTurnResponse(turn);
            AppendContinuationItems(
                turn.ContinuationItems,
                continuationItems,
                exchanges.Count);

            if (!string.IsNullOrWhiteSpace(turn.FinalAnswer))
            {
                return new AgentOrchestrationResult(
                    turn.FinalAnswer.Trim(),
                    turnNumber,
                    usages.ToArray());
            }

            if (turnNumber == _options.MaxTurns)
            {
                throw CreateIncompleteException(
                    "The agent reached its turn limit before producing an answer.");
            }

            var calls = turn.ToolCalls.ToArray();

            if (calls.Length > _options.MaxToolCallsPerTurn
                || usages.Count + calls.Length > _options.MaxTotalToolCalls)
            {
                throw CreateIncompleteException(
                    "The agent exceeded its configured tool-call budget.");
            }

            foreach (var call in calls)
            {
                ValidateToolCall(call, allowedToolNames, usedCallIds);
            }

            foreach (var call in calls)
            {
                var output = await agentToolExecutor.ExecuteAsync(
                    workspaceId,
                    call,
                    cancellationToken);
                ValidateToolOutput(output);
                exchanges.Add(new AgentToolExchange(call, output));
                usages.Add(new AgentToolUsage(
                    call.CallId,
                    call.ToolName,
                    output.Succeeded));
            }
        }

        throw CreateIncompleteException(
            "The agent did not produce a final answer.");
    }

    private HashSet<string> ValidateToolCatalog(
        IReadOnlyList<AgentToolDefinition> tools)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tool in tools)
        {
            if (tool is null
                || string.IsNullOrWhiteSpace(tool.Name)
                || tool.Name.Length > MaxToolNameLength
                || !ToolNamePattern().IsMatch(tool.Name)
                || string.IsNullOrWhiteSpace(tool.Description)
                || tool.Description.Length > MaxToolDescriptionLength
                || string.IsNullOrWhiteSpace(tool.InputJsonSchema)
                || tool.InputJsonSchema.Length > MaxToolSchemaLength
                || !IsJsonObject(tool.InputJsonSchema)
                || !names.Add(tool.Name))
            {
                throw new InvalidOperationException(
                    "The agent tool catalog contains an invalid definition.");
            }
        }

        return names;
    }

    private void ValidateTurnResponse(AgentTurnResponse? turn)
    {
        if (turn is null || turn.ToolCalls is null)
        {
            throw CreateInvalidResponseException();
        }

        var hasAnswer = !string.IsNullOrWhiteSpace(turn.FinalAnswer);
        var hasToolCalls = turn.ToolCalls.Count > 0;

        if (hasAnswer == hasToolCalls
            || hasAnswer && turn.FinalAnswer!.Length > _options.MaxFinalAnswerLength)
        {
            throw CreateInvalidResponseException();
        }
    }

    private void ValidateToolCall(
        AgentToolCall? call,
        IReadOnlySet<string> allowedToolNames,
        ISet<string> usedCallIds)
    {
        if (call is null
            || string.IsNullOrWhiteSpace(call.CallId)
            || call.CallId.Length > MaxCallIdLength
            || string.IsNullOrWhiteSpace(call.ToolName)
            || !allowedToolNames.Contains(call.ToolName)
            || string.IsNullOrWhiteSpace(call.ArgumentsJson)
            || call.ArgumentsJson.Length > _options.MaxToolArgumentsLength
            || !IsJsonObject(call.ArgumentsJson)
            || !usedCallIds.Add(call.CallId))
        {
            throw CreateInvalidResponseException();
        }
    }

    private void ValidateToolOutput(AgentToolExecutionOutput? output)
    {
        if (output is null
            || string.IsNullOrWhiteSpace(output.Content)
            || output.Content.Length > _options.MaxToolOutputLength)
        {
            throw new InvalidOperationException(
                "An agent tool returned output outside the accepted contract.");
        }
    }

    private static void AppendContinuationItems(
        IReadOnlyList<AgentContinuationItem>? newItems,
        ICollection<AgentContinuationItem> allItems,
        int currentToolExchangeCount)
    {
        if (newItems is null)
        {
            return;
        }

        if (allItems.Count + newItems.Count > MaxContinuationItems)
        {
            throw CreateInvalidResponseException();
        }

        foreach (var item in newItems)
        {
            if (item is null
                || item.BeforeToolExchangeIndex != currentToolExchangeCount
                || string.IsNullOrWhiteSpace(item.OpaqueValue)
                || item.OpaqueValue.Length > MaxContinuationItemLength)
            {
                throw CreateInvalidResponseException();
            }

            allItems.Add(item);
        }

        if (allItems.Sum(item => item.OpaqueValue.Length)
            > MaxTotalContinuationLength)
        {
            throw CreateInvalidResponseException();
        }
    }

    private static bool IsJsonObject(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static LlmProviderException CreateInvalidResponseException()
    {
        return new LlmProviderException(
            LlmProviderFailureKind.InvalidResponse,
            "The AI provider returned an invalid agent turn.");
    }

    private static LlmProviderException CreateIncompleteException(string message)
    {
        return new LlmProviderException(
            LlmProviderFailureKind.Incomplete,
            message);
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ToolNamePattern();
}
