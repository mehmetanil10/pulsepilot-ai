using FluentValidation;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.AI;

public sealed class FeedbackAnalysisResultValidator : AbstractValidator<FeedbackAnalysisResult>
{
    public FeedbackAnalysisResultValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(result => result.Category).IsInEnum();
        RuleFor(result => result.Component).IsInEnum();
        RuleFor(result => result.Severity)
            .InclusiveBetween(
                FeedbackAnalysis.MinimumSeverity,
                FeedbackAnalysis.MaximumSeverity);
        RuleFor(result => result.Sentiment).IsInEnum();
        RuleFor(result => result.Summary)
            .NotEmpty()
            .MaximumLength(FeedbackAnalysis.MaxSummaryLength);
        RuleFor(result => result.SuggestedAction)
            .NotEmpty()
            .MaximumLength(FeedbackAnalysis.MaxSuggestedActionLength);
        RuleFor(result => result.Confidence)
            .InclusiveBetween(
                FeedbackAnalysis.MinimumConfidence,
                FeedbackAnalysis.MaximumConfidence);
    }
}
