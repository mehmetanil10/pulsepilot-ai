namespace PulsePilot.Application.Copilot;

public sealed record CopilotChatResponse(
    string Answer,
    int ModelTurnCount,
    int ToolCallCount,
    IReadOnlyList<CopilotToolUsageResponse> ToolUsages);

public sealed record CopilotToolUsageResponse(
    string ToolName,
    bool Succeeded);
