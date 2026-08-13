using PulsePilot.Application.Abstractions.AI;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.Application.Agents;

internal sealed class EmptyAgentToolCatalog : IAgentToolCatalog
{
    public IReadOnlyList<AgentToolDefinition> ListTools() => [];
}

internal sealed class DisabledAgentToolExecutor : IAgentToolExecutor
{
    public Task<AgentToolExecutionOutput> ExecuteAsync(
        Guid workspaceId,
        AgentToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Agent tool execution is not configured.");
    }
}

internal sealed class UnavailableAgentTurnClient : IAgentTurnClient
{
    public Task<AgentTurnResponse> CreateTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new LlmProviderException(
            LlmProviderFailureKind.NotConfigured,
            "Agent turn generation is not configured.");
    }
}
