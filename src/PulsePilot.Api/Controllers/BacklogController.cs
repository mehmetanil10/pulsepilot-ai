using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulsePilot.Application.Backlog;

namespace PulsePilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/backlog")]
public sealed class BacklogController(IBacklogItemService backlogItemService)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<BacklogItemListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BacklogItemListResponse>> List(
        [FromQuery] BacklogItemQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await backlogItemService.ListAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<BacklogItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BacklogItemResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await backlogItemService.GetByIdAsync(id, cancellationToken));
    }
}
