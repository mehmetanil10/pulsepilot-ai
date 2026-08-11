using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PulsePilot.Application.Feedback;

namespace PulsePilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/feedback")]
public sealed class FeedbackController(IFeedbackService feedbackService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<FeedbackResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeedbackResponse>> Create(
        CreateFeedbackCommand command,
        CancellationToken cancellationToken)
    {
        var response = await feedbackService.CreateAsync(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType<FeedbackListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FeedbackListResponse>> List(
        [FromQuery] ListFeedbackQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await feedbackService.ListAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<FeedbackResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await feedbackService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<FeedbackResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedbackResponse>> Update(
        Guid id,
        UpdateFeedbackCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await feedbackService.UpdateAsync(id, command, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await feedbackService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
