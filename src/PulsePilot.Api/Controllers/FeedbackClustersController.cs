using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulsePilot.Application.FeedbackClusters;

namespace PulsePilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clusters")]
public sealed class FeedbackClustersController(
    IFeedbackClusterService clusterService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<FeedbackClusterListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeedbackClusterListResponse>> List(
        [FromQuery] FeedbackClusterQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await clusterService.ListAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<FeedbackClusterDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackClusterDetailResponse>> GetById(
        Guid id,
        [FromQuery] FeedbackClusterQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await clusterService.GetByIdAsync(id, query, cancellationToken));
    }
}
