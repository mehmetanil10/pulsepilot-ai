using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulsePilot.Api.Authentication;
using PulsePilot.Application.Authentication;

namespace PulsePilot.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthenticationResponse>> Register(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var response = await authService.RegisterAsync(command, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthenticationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticationResponse>> Login(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.LoginAsync(command, cancellationToken));
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> GetCurrentUser()
    {
        if (!Guid.TryParse(User.FindFirst(TokenClaimNames.Subject)?.Value, out var userId)
            || !Guid.TryParse(
                User.FindFirst(TokenClaimNames.WorkspaceId)?.Value,
                out var workspaceId))
        {
            return Unauthorized();
        }

        return Ok(new CurrentUserResponse(
            userId,
            User.FindFirst(TokenClaimNames.Email)?.Value ?? string.Empty,
            User.FindFirst(TokenClaimNames.Name)?.Value ?? string.Empty,
            workspaceId,
            User.FindFirst(TokenClaimNames.Role)?.Value ?? string.Empty));
    }
}
