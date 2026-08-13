namespace PulsePilot.Application.Agents;

public sealed record AgentToolDefinition(
    string Name,
    string Description,
    string InputJsonSchema);

public sealed record AgentToolCall(
    string CallId,
    string ToolName,
    string ArgumentsJson);

public sealed record AgentToolExecutionOutput(
    bool Succeeded,
    string Content);

public sealed record AgentToolExchange(
    AgentToolCall Call,
    AgentToolExecutionOutput Output);

public sealed record AgentContinuationItem(
    int BeforeToolExchangeIndex,
    string OpaqueValue);

public sealed record AgentTurnRequest(
    string UserMessage,
    IReadOnlyList<AgentToolDefinition> AvailableTools,
    IReadOnlyList<AgentToolExchange> PreviousToolExchanges,
    IReadOnlyList<AgentContinuationItem>? PreviousContinuationItems = null);

public sealed record AgentTurnResponse(
    string? FinalAnswer,
    IReadOnlyList<AgentToolCall> ToolCalls,
    IReadOnlyList<AgentContinuationItem>? ContinuationItems = null);

public sealed record AgentToolUsage(
    string CallId,
    string ToolName,
    bool Succeeded);

public sealed record AgentOrchestrationResult(
    string Answer,
    int ModelTurnCount,
    IReadOnlyList<AgentToolUsage> ToolUsages)
{
    public int ToolCallCount => ToolUsages.Count;
}
