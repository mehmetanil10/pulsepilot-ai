using PulsePilot.Application.Agents;

namespace PulsePilot.Application.Abstractions.AI;

public interface IAgentTurnClient
{
    Task<AgentTurnResponse> CreateTurnAsync(
        AgentTurnRequest request,
        CancellationToken cancellationToken = default);
}
