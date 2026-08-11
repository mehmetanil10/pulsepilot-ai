using PulsePilot.Application.Common.Exceptions;

namespace PulsePilot.Application.FeedbackProcessing;

public enum FeedbackAnalysisProcessStatus
{
    NoWork = 1,
    Succeeded = 2,
    Failed = 3,
    Abandoned = 4,
}

public sealed record FeedbackAnalysisProcessResult(
    FeedbackAnalysisProcessStatus Status,
    Guid? FeedbackId,
    Guid? WorkspaceId,
    int Attempts,
    TimeSpan Duration,
    LlmProviderFailureKind? FailureKind)
{
    public static FeedbackAnalysisProcessResult NoWork { get; } = new(
        FeedbackAnalysisProcessStatus.NoWork,
        null,
        null,
        0,
        TimeSpan.Zero,
        null);
}
