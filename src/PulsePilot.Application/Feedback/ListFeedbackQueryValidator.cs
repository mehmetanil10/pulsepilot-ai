using FluentValidation;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Application.Feedback;

public sealed class ListFeedbackQueryValidator : AbstractValidator<ListFeedbackQuery>
{
    public const int MaximumPageSize = 100;
    public const int MaximumSearchLength = 200;

    public ListFeedbackQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaximumPageSize);
        RuleFor(query => query.Source)
            .Must(source => !source.HasValue || Enum.IsDefined(source.Value))
            .WithMessage("Source has an unsupported value.");
        RuleFor(query => query.ProcessingStatus)
            .Must(status => !status.HasValue || Enum.IsDefined(status.Value))
            .WithMessage("ProcessingStatus has an unsupported value.");
        RuleFor(query => query.Category)
            .Must(value => !value.HasValue || Enum.IsDefined(value.Value))
            .WithMessage("Category has an unsupported value.");
        RuleFor(query => query.Component)
            .Must(value => !value.HasValue || Enum.IsDefined(value.Value))
            .WithMessage("Component has an unsupported value.");
        RuleFor(query => query.Sentiment)
            .Must(value => !value.HasValue || Enum.IsDefined(value.Value))
            .WithMessage("Sentiment has an unsupported value.");
        RuleFor(query => query.Severity)
            .InclusiveBetween(FeedbackAnalysis.MinimumSeverity, FeedbackAnalysis.MaximumSeverity)
            .When(query => query.Severity.HasValue);
        RuleFor(query => query.Search)
            .MaximumLength(MaximumSearchLength);
        RuleFor(query => query.DateTo)
            .Must(value => !value.HasValue || value.Value < DateOnly.MaxValue)
            .WithMessage("DateTo is outside the supported range.");
        RuleFor(query => query)
            .Must(query => !query.DateFrom.HasValue
                || !query.DateTo.HasValue
                || query.DateFrom.Value <= query.DateTo.Value)
            .WithMessage("DateFrom must be on or before DateTo.");
        RuleFor(query => query)
            .Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page is outside the supported range.");
    }
}
