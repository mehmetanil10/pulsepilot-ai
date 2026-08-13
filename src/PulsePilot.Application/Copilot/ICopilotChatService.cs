namespace PulsePilot.Application.Copilot;

public interface ICopilotChatService
{
    Task<CopilotChatResponse> ChatAsync(
        CopilotChatCommand command,
        CancellationToken cancellationToken = default);
}
