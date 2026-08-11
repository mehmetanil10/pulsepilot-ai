namespace PulsePilot.Application.Authentication;

public sealed record AuthenticationResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Email,
    string DisplayName,
    Guid WorkspaceId,
    string WorkspaceName,
    string Role);
