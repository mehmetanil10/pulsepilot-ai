using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulsePilot.Application.Reports;
using PulsePilot.Application.Tools;

namespace PulsePilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController(IWeeklyReportService weeklyReportService)
    : ControllerBase
{
    [HttpPost("weekly")]
    [ProducesResponseType<GenerateReportToolResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GenerateReportToolResult>> GenerateWeekly(
        GenerateWeeklyReportCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await weeklyReportService.GenerateAsync(command, cancellationToken));
    }
}
