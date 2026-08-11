namespace PulsePilot.Api.Authentication;

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid WorkspaceId,
    string Role);
