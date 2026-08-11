using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Feedback;

public sealed record UpdateFeedbackCommand(
    string? Title,
    string Content,
    FeedbackSource Source,
    string? CustomerName,
    string? CustomerEmail);
