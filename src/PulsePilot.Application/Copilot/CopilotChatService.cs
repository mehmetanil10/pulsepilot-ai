using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Agents;

namespace PulsePilot.Application.Copilot;

internal sealed class CopilotChatService(
    IAgentOrchestrator agentOrchestrator,
    ICurrentUserContext currentUser) : ICopilotChatService
{
    public async Task<CopilotChatResponse> ChatAsync(
        CopilotChatCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await agentOrchestrator.RunAsync(
            currentUser.WorkspaceId,
            command.Message,
            cancellationToken);

        return new CopilotChatResponse(
            result.Answer,
            result.ModelTurnCount,
            result.ToolCallCount,
            result.ToolUsages
                .Select(usage => new CopilotToolUsageResponse(
                    usage.ToolName,
                    usage.Succeeded))
                .ToArray());
    }
}
