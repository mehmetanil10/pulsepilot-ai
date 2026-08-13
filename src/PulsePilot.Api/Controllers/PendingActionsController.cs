using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulsePilot.Application.Actions;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/actions")]
public sealed class PendingActionsController(
    IPendingActionService pendingActionService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PendingActionListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PendingActionListResponse>> List(
        [FromQuery] PendingActionQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await pendingActionService.ListAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PendingActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PendingActionResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await pendingActionService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = nameof(WorkspaceRole.Admin))]
    [ProducesResponseType<PendingActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PendingActionResponse>> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await pendingActionService.ApproveAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = nameof(WorkspaceRole.Admin))]
    [ProducesResponseType<PendingActionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PendingActionResponse>> Reject(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await pendingActionService.RejectAsync(id, cancellationToken));
    }
}
