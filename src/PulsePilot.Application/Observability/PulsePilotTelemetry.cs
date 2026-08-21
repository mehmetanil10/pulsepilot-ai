using System.Diagnostics;
using System.Diagnostics.Metrics;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.FeedbackProcessing;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Observability;

public static class PulsePilotTelemetry
{
    public const string ActivitySourceName = "PulsePilot.Application";
    public const string MeterName = "PulsePilot.Application";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> FeedbackProcessingCounter = Meter.CreateCounter<long>(
        "pulsepilot.feedback.processing.count",
        description: "Number of feedback processing outcomes.");
    private static readonly Histogram<double> FeedbackProcessingDuration = Meter.CreateHistogram<double>(
        "pulsepilot.feedback.processing.duration",
        unit: "s",
        description: "End-to-end feedback processing duration.");
    private static readonly Counter<long> AiAttemptCounter = Meter.CreateCounter<long>(
        "pulsepilot.ai.attempt.count",
        description: "Number of AI provider attempts.");
    private static readonly Counter<long> PendingActionReviewCounter = Meter.CreateCounter<long>(
        "pulsepilot.pending_action.review.count",
        description: "Number of human review outcomes for pending actions.");

    public static Activity? StartFeedbackProcessing(FeedbackSource source)
    {
        var activity = ActivitySource.StartActivity("feedback.process", ActivityKind.Internal);
        activity?.SetTag("pulsepilot.feedback.source", NormalizeSource(source));

        return activity;
    }

    public static void RecordFeedbackProcessing(
        FeedbackAnalysisProcessStatus status,
        TimeSpan duration,
        int attempts,
        LlmProviderFailureKind? failureKind = null)
    {
        var outcome = status.ToString().ToLowerInvariant();
        var failure = failureKind?.ToString().ToLowerInvariant() ?? "none";
        var tags = new TagList
        {
            { "pulsepilot.outcome", outcome },
            { "pulsepilot.failure.kind", failure },
        };

        FeedbackProcessingCounter.Add(1, tags);
        FeedbackProcessingDuration.Record(duration.TotalSeconds, tags);

        var activity = Activity.Current;
        activity?.SetTag("pulsepilot.outcome", outcome);
        activity?.SetTag("pulsepilot.attempts", attempts);
        activity?.SetTag("pulsepilot.failure.kind", failure);
        activity?.SetStatus(
            status == FeedbackAnalysisProcessStatus.Failed
                ? ActivityStatusCode.Error
                : ActivityStatusCode.Ok);
    }

    public static Activity? StartAiOperation(string operation)
    {
        var activity = ActivitySource.StartActivity($"ai.{operation}", ActivityKind.Internal);
        activity?.SetTag("pulsepilot.ai.operation", operation);

        return activity;
    }

    public static void RecordAiAttempt(
        string operation,
        string outcome,
        LlmProviderFailureKind? failureKind = null)
    {
        var failure = failureKind?.ToString().ToLowerInvariant() ?? "none";
        var tags = new TagList
        {
            { "pulsepilot.ai.operation", operation },
            { "pulsepilot.outcome", outcome },
            { "pulsepilot.failure.kind", failure },
        };

        AiAttemptCounter.Add(1, tags);
    }

    public static void RecordAiOperationFailed(
        Activity? activity,
        int attempts,
        LlmProviderFailureKind failureKind)
    {
        activity?.SetTag("pulsepilot.attempts", attempts);
        activity?.SetTag("pulsepilot.outcome", "failed");
        activity?.SetTag(
            "pulsepilot.failure.kind",
            failureKind.ToString().ToLowerInvariant());
        activity?.SetStatus(ActivityStatusCode.Error);
    }

    public static Activity? StartPendingActionReview(PendingActionStatus decision)
    {
        var activity = ActivitySource.StartActivity("pending_action.review", ActivityKind.Internal);
        activity?.SetTag("pulsepilot.review.decision", decision.ToString().ToLowerInvariant());

        return activity;
    }

    public static void RecordPendingActionReview(
        PendingActionStatus decision,
        PendingActionType actionType,
        string outcome)
    {
        var tags = new TagList
        {
            { "pulsepilot.review.decision", decision.ToString().ToLowerInvariant() },
            { "pulsepilot.action.type", actionType.ToString().ToLowerInvariant() },
            { "pulsepilot.outcome", outcome },
        };

        PendingActionReviewCounter.Add(1, tags);
    }

    private static string NormalizeSource(FeedbackSource source)
    {
        return source switch
        {
            FeedbackSource.Api => "api",
            FeedbackSource.AppReview => "app_review",
            FeedbackSource.Email => "email",
            FeedbackSource.Manual => "manual",
            FeedbackSource.Support => "support",
            FeedbackSource.Survey => "survey",
            _ => "other",
        };
    }
}
