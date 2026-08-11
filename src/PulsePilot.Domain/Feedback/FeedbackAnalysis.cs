using PulsePilot.Domain.Common;

namespace PulsePilot.Domain.Feedback;

public sealed class FeedbackAnalysis : AuditableEntity
{
    public const int MinimumSeverity = 1;
    public const int MaximumSeverity = 5;
    public const decimal MinimumConfidence = 0m;
    public const decimal MaximumConfidence = 1m;
    public const int MaxSummaryLength = 2_000;
    public const int MaxSuggestedActionLength = 2_000;

    private FeedbackAnalysis()
    {
    }

    private FeedbackAnalysis(
        Guid id,
        Guid workspaceId,
        Guid feedbackId,
        FeedbackCategory category,
        FeedbackComponent component,
        int severity,
        FeedbackSentiment sentiment,
        string summary,
        string suggestedAction,
        decimal confidence,
        DateTimeOffset analyzedAt)
        : base(id, analyzedAt)
    {
        WorkspaceId = Guard.NotEmpty(workspaceId, nameof(workspaceId));
        FeedbackId = Guard.NotEmpty(feedbackId, nameof(feedbackId));
        ApplyAnalysis(
            category,
            component,
            severity,
            sentiment,
            summary,
            suggestedAction,
            confidence);
    }

    public Guid WorkspaceId { get; private set; }

    public Guid FeedbackId { get; private set; }

    public FeedbackCategory Category { get; private set; }

    public FeedbackComponent Component { get; private set; }

    public int Severity { get; private set; }

    public FeedbackSentiment Sentiment { get; private set; }

    public string Summary { get; private set; } = null!;

    public string SuggestedAction { get; private set; } = null!;

    public decimal Confidence { get; private set; }

    public static FeedbackAnalysis Create(
        Guid workspaceId,
        Guid feedbackId,
        FeedbackCategory category,
        FeedbackComponent component,
        int severity,
        FeedbackSentiment sentiment,
        string summary,
        string suggestedAction,
        decimal confidence,
        DateTimeOffset analyzedAt)
    {
        return new FeedbackAnalysis(
            Guid.CreateVersion7(),
            workspaceId,
            feedbackId,
            category,
            component,
            severity,
            sentiment,
            summary,
            suggestedAction,
            confidence,
            analyzedAt);
    }

    public void ReplaceResult(
        FeedbackCategory category,
        FeedbackComponent component,
        int severity,
        FeedbackSentiment sentiment,
        string summary,
        string suggestedAction,
        decimal confidence,
        DateTimeOffset analyzedAt)
    {
        var validated = ValidateAnalysis(
            category,
            component,
            severity,
            sentiment,
            summary,
            suggestedAction,
            confidence);

        MarkUpdated(analyzedAt);
        SetAnalysis(validated);
    }

    private void ApplyAnalysis(
        FeedbackCategory category,
        FeedbackComponent component,
        int severity,
        FeedbackSentiment sentiment,
        string summary,
        string suggestedAction,
        decimal confidence)
    {
        SetAnalysis(ValidateAnalysis(
            category,
            component,
            severity,
            sentiment,
            summary,
            suggestedAction,
            confidence));
    }

    private void SetAnalysis(ValidatedAnalysis result)
    {
        Category = result.Category;
        Component = result.Component;
        Severity = result.Severity;
        Sentiment = result.Sentiment;
        Summary = result.Summary;
        SuggestedAction = result.SuggestedAction;
        Confidence = result.Confidence;
    }

    private static ValidatedAnalysis ValidateAnalysis(
        FeedbackCategory category,
        FeedbackComponent component,
        int severity,
        FeedbackSentiment sentiment,
        string summary,
        string suggestedAction,
        decimal confidence)
    {
        var validatedCategory = Guard.DefinedEnum(category, nameof(category));
        var validatedComponent = Guard.DefinedEnum(component, nameof(component));
        var validatedSentiment = Guard.DefinedEnum(sentiment, nameof(sentiment));
        var validatedSummary = Guard.RequiredText(
            summary,
            nameof(summary),
            MaxSummaryLength);
        var validatedSuggestedAction = Guard.RequiredText(
            suggestedAction,
            nameof(suggestedAction),
            MaxSuggestedActionLength);

        if (severity is < MinimumSeverity or > MaximumSeverity)
        {
            throw new DomainException(
                $"severity must be between {MinimumSeverity} and {MaximumSeverity}.");
        }

        if (confidence is < MinimumConfidence or > MaximumConfidence)
        {
            throw new DomainException(
                $"confidence must be between {MinimumConfidence} and {MaximumConfidence}.");
        }

        return new ValidatedAnalysis(
            validatedCategory,
            validatedComponent,
            severity,
            validatedSentiment,
            validatedSummary,
            validatedSuggestedAction,
            confidence);
    }

    private sealed record ValidatedAnalysis(
        FeedbackCategory Category,
        FeedbackComponent Component,
        int Severity,
        FeedbackSentiment Sentiment,
        string Summary,
        string SuggestedAction,
        decimal Confidence);
}
