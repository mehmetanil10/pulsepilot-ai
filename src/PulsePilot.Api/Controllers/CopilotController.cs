using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulsePilot.Application.Copilot;

namespace PulsePilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/copilot")]
public sealed class CopilotController(ICopilotChatService copilotChatService)
    : ControllerBase
{
    [HttpPost("chat")]
    [ProducesResponseType<CopilotChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CopilotChatResponse>> Chat(
        CopilotChatCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await copilotChatService.ChatAsync(command, cancellationToken));
    }
}
