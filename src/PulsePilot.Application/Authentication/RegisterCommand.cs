namespace PulsePilot.Application.Authentication;

public sealed record RegisterCommand(
    string Email,
    string DisplayName,
    string Password,
    string WorkspaceName);
