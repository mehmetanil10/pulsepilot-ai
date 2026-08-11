using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Authentication;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Infrastructure.Authentication;

internal sealed class JwtAccessTokenGenerator(IOptions<JwtOptions> options)
    : IAccessTokenGenerator
{
    public AccessToken Generate(
        User user,
        WorkspaceMember workspaceMember,
        DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(workspaceMember);

        var jwtOptions = options.Value;
        var expiresAt = issuedAt.AddMinutes(jwtOptions.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(TokenClaimNames.Subject, user.Id.ToString("D")),
            new(TokenClaimNames.Email, user.Email),
            new(TokenClaimNames.Name, user.DisplayName),
            new(TokenClaimNames.WorkspaceId, workspaceMember.WorkspaceId.ToString("D")),
            new(TokenClaimNames.Role, workspaceMember.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString("D")),
        };
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret));
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = jwtOptions.Issuer,
            Audience = jwtOptions.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                signingKey,
                SecurityAlgorithms.HmacSha256),
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);

        return new AccessToken(tokenHandler.WriteToken(securityToken), expiresAt);
    }
}
