using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulsePilot.Application.Dashboard;

namespace PulsePilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType<DashboardSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(
        [FromQuery] DashboardSummaryQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await dashboardService.GetSummaryAsync(query, cancellationToken));
    }

    [HttpGet("trending")]
    [ProducesResponseType<DashboardTrendingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardTrendingResponse>> GetTrending(
        [FromQuery] DashboardTrendingQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await dashboardService.GetTrendingAsync(query, cancellationToken));
    }
}
