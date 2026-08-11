namespace PulsePilot.Application.Abstractions.Authentication;

public interface ICurrentUserContext
{
    Guid UserId { get; }

    Guid WorkspaceId { get; }

    string Role { get; }
}
