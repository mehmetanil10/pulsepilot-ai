namespace PulsePilot.Application.Agents;

public interface IAgentToolCatalog
{
    IReadOnlyList<AgentToolDefinition> ListTools();
}
