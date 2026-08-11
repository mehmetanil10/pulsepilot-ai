using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Application.Abstractions.Authentication;

public interface IAccessTokenGenerator
{
    AccessToken Generate(
        User user,
        WorkspaceMember workspaceMember,
        DateTimeOffset issuedAt);
}
