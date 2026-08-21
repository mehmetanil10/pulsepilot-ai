using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PulsePilot.Api.Authentication;
using PulsePilot.Api.Protection;
using PulsePilot.Application.Abstractions.Authentication;
using PulsePilot.Application.Authentication;

namespace PulsePilot.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    ICurrentUserContext currentUser) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(ApiRateLimitPolicies.Authentication)]
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
    [EnableRateLimiting(ApiRateLimitPolicies.Authentication)]
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
        return Ok(new CurrentUserResponse(
            currentUser.UserId,
            User.FindFirst(TokenClaimNames.Email)?.Value ?? string.Empty,
            User.FindFirst(TokenClaimNames.Name)?.Value ?? string.Empty,
            currentUser.WorkspaceId,
            currentUser.Role));
    }
}
