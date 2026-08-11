using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Authentication;
using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.Api.Authentication;

internal sealed class HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserContext
{
    public Guid UserId => GetRequiredGuidClaim(TokenClaimNames.Subject);

    public Guid WorkspaceId => GetRequiredGuidClaim(TokenClaimNames.WorkspaceId);

    public string Role => GetRequiredClaim(TokenClaimNames.Role);

    private Guid GetRequiredGuidClaim(string claimType)
    {
        return Guid.TryParse(GetRequiredClaim(claimType), out var value)
            ? value
            : throw new InvalidCredentialsException();
    }

    private string GetRequiredClaim(string claimType)
    {
        return httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value
            ?? throw new InvalidCredentialsException();
    }
}
