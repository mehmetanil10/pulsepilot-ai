namespace PulsePilot.Application.Agents;

public interface IAgentToolExecutor
{
    Task<AgentToolExecutionOutput> ExecuteAsync(
        Guid workspaceId,
        AgentToolCall toolCall,
        CancellationToken cancellationToken = default);
}
