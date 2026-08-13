namespace PulsePilot.Application.Agents;

public interface IAgentOrchestrator
{
    Task<AgentOrchestrationResult> RunAsync(
        Guid workspaceId,
        string userMessage,
        CancellationToken cancellationToken = default);
}
