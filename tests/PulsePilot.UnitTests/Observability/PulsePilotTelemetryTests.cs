using System.Diagnostics;
using System.Diagnostics.Metrics;
using PulsePilot.Application.Common.Exceptions;
using PulsePilot.Application.FeedbackProcessing;
using PulsePilot.Application.Observability;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.UnitTests.Observability;

public sealed class PulsePilotTelemetryTests
{
    public static TheoryData<FeedbackSource, string> FeedbackSources => new()
    {
        { FeedbackSource.Api, "api" },
        { FeedbackSource.AppReview, "app_review" },
        { FeedbackSource.Email, "email" },
        { FeedbackSource.Manual, "manual" },
        { FeedbackSource.Support, "support" },
        { FeedbackSource.Survey, "survey" },
        { (FeedbackSource)999, "other" },
    };

    [Fact]
    public void FeedbackProcessing_EmitsTraceWithBoundedSafeAttributes()
    {
        Activity? stoppedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PulsePilotTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => stoppedActivity = activity,
        };
        ActivitySource.AddActivityListener(listener);

        using (PulsePilotTelemetry.StartFeedbackProcessing(FeedbackSource.Support))
        {
            PulsePilotTelemetry.RecordFeedbackProcessing(
                FeedbackAnalysisProcessStatus.Succeeded,
                TimeSpan.FromMilliseconds(125),
                attempts: 2);
        }

        Assert.NotNull(stoppedActivity);
        Assert.Equal("feedback.process", stoppedActivity.OperationName);
        Assert.Equal(ActivityStatusCode.Ok, stoppedActivity.Status);
        Assert.Equal("support", stoppedActivity.GetTagItem("pulsepilot.feedback.source"));
        Assert.Equal("succeeded", stoppedActivity.GetTagItem("pulsepilot.outcome"));
        Assert.Equal(2, stoppedActivity.GetTagItem("pulsepilot.attempts"));
        Assert.DoesNotContain(
            stoppedActivity.TagObjects,
            tag => tag.Value?.ToString()?.Contains('@', StringComparison.Ordinal) == true);
    }

    [Fact]
    public void FeedbackProcessing_EmitsCountAndDurationMetrics()
    {
        var measurements = new List<Measurement>();
        using var listener = CreateMeterListener(measurements);

        PulsePilotTelemetry.RecordFeedbackProcessing(
            FeedbackAnalysisProcessStatus.Failed,
            TimeSpan.FromSeconds(1.5),
            attempts: 3,
            LlmProviderFailureKind.ProviderUnavailable);

        var count = Assert.Single(measurements, measurement =>
            measurement.Name == "pulsepilot.feedback.processing.count");
        var duration = Assert.Single(measurements, measurement =>
            measurement.Name == "pulsepilot.feedback.processing.duration");

        Assert.Equal(1, count.Value);
        Assert.Equal(1.5, duration.Value);
        Assert.Equal("failed", count.Tags["pulsepilot.outcome"]);
        Assert.Equal("providerunavailable", count.Tags["pulsepilot.failure.kind"]);
    }

    [Fact]
    public void AiAttempt_EmitsOnlyBoundedOperationOutcomeAndFailureTags()
    {
        var measurements = new List<Measurement>();
        using var listener = CreateMeterListener(measurements);

        PulsePilotTelemetry.RecordAiAttempt(
            "embedding_generation",
            "retryable_failure",
            LlmProviderFailureKind.ProviderUnavailable);

        var attempt = Assert.Single(measurements);
        Assert.Equal("pulsepilot.ai.attempt.count", attempt.Name);
        Assert.Equal("embedding_generation", attempt.Tags["pulsepilot.ai.operation"]);
        Assert.Equal("retryable_failure", attempt.Tags["pulsepilot.outcome"]);
        Assert.Equal("providerunavailable", attempt.Tags["pulsepilot.failure.kind"]);
        Assert.Equal(3, attempt.Tags.Count);
    }

    [Fact]
    public void PendingActionReview_EmitsHumanDecisionMetric()
    {
        var measurements = new List<Measurement>();
        using var listener = CreateMeterListener(measurements);

        PulsePilotTelemetry.RecordPendingActionReview(
            PendingActionStatus.Approved,
            PendingActionType.CreateEngineeringIssue,
            "executed");

        var review = Assert.Single(measurements);
        Assert.Equal("pulsepilot.pending_action.review.count", review.Name);
        Assert.Equal("approved", review.Tags["pulsepilot.review.decision"]);
        Assert.Equal("createengineeringissue", review.Tags["pulsepilot.action.type"]);
        Assert.Equal("executed", review.Tags["pulsepilot.outcome"]);
    }

    [Theory]
    [MemberData(nameof(FeedbackSources))]
    public void FeedbackProcessingTrace_NormalizesEverySourceToBoundedValue(
        FeedbackSource source,
        string expected)
    {
        Activity? stoppedActivity = null;
        using var listener = CreateActivityListener(activity => stoppedActivity = activity);

        using (PulsePilotTelemetry.StartFeedbackProcessing(source))
        {
        }

        Assert.NotNull(stoppedActivity);
        Assert.Equal(expected, stoppedActivity.GetTagItem("pulsepilot.feedback.source"));
    }

    [Fact]
    public void AiOperation_EmitsLogicalSpanWithoutProviderEndpointAttributes()
    {
        Activity? stoppedActivity = null;
        using var listener = CreateActivityListener(activity => stoppedActivity = activity);

        using (var activity = PulsePilotTelemetry.StartAiOperation("feedback_analysis"))
        {
            activity?.SetStatus(ActivityStatusCode.Error);
        }

        Assert.NotNull(stoppedActivity);
        Assert.Equal("ai.feedback_analysis", stoppedActivity.OperationName);
        Assert.Equal(ActivityKind.Internal, stoppedActivity.Kind);
        Assert.Equal("feedback_analysis", stoppedActivity.GetTagItem("pulsepilot.ai.operation"));
        Assert.Null(stoppedActivity.GetTagItem("server.address"));
    }

    [Fact]
    public void AiOperationFailure_AnnotatesSpanWithSafeFailureMetadata()
    {
        Activity? stoppedActivity = null;
        using var listener = CreateActivityListener(activity => stoppedActivity = activity);

        using (var activity = PulsePilotTelemetry.StartAiOperation("embedding_generation"))
        {
            PulsePilotTelemetry.RecordAiOperationFailed(
                activity,
                attempts: 3,
                LlmProviderFailureKind.ProviderUnavailable);
        }

        Assert.NotNull(stoppedActivity);
        Assert.Equal(ActivityStatusCode.Error, stoppedActivity.Status);
        Assert.Equal(3, stoppedActivity.GetTagItem("pulsepilot.attempts"));
        Assert.Equal("failed", stoppedActivity.GetTagItem("pulsepilot.outcome"));
        Assert.Equal(
            "providerunavailable",
            stoppedActivity.GetTagItem("pulsepilot.failure.kind"));
    }

    [Fact]
    public void TelemetryWithoutListeners_RemainsSafeNoOp()
    {
        Assert.Null(PulsePilotTelemetry.StartFeedbackProcessing(FeedbackSource.Manual));
        Assert.Null(PulsePilotTelemetry.StartAiOperation("feedback_analysis"));
        Assert.Null(PulsePilotTelemetry.StartPendingActionReview(PendingActionStatus.Approved));

        PulsePilotTelemetry.RecordFeedbackProcessing(
            FeedbackAnalysisProcessStatus.Succeeded,
            TimeSpan.Zero,
            attempts: 1);
        PulsePilotTelemetry.RecordAiAttempt("feedback_analysis", "succeeded");
        PulsePilotTelemetry.RecordAiOperationFailed(
            activity: null,
            attempts: 1,
            LlmProviderFailureKind.ProviderFailure);
    }

    [Fact]
    public void PendingActionReview_EmitsHumanInTheLoopSpan()
    {
        Activity? stoppedActivity = null;
        using var listener = CreateActivityListener(activity => stoppedActivity = activity);

        using (PulsePilotTelemetry.StartPendingActionReview(PendingActionStatus.Rejected))
        {
        }

        Assert.NotNull(stoppedActivity);
        Assert.Equal("pending_action.review", stoppedActivity.OperationName);
        Assert.Equal("rejected", stoppedActivity.GetTagItem("pulsepilot.review.decision"));
    }

    private static MeterListener CreateMeterListener(List<Measurement> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == PulsePilotTelemetry.MeterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, ToDictionary(tags))));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add(new Measurement(instrument.Name, value, ToDictionary(tags))));
        listener.Start();

        return listener;
    }

    private static ActivityListener CreateActivityListener(Action<Activity> onStopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == PulsePilotTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = onStopped,
        };
        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    private static Dictionary<string, object?> ToDictionary(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var tag in tags)
        {
            result[tag.Key] = tag.Value;
        }

        return result;
    }

    private sealed record Measurement(
        string Name,
        double Value,
        Dictionary<string, object?> Tags);
}
