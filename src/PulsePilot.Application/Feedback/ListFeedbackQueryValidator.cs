using FluentValidation;

namespace PulsePilot.Application.Feedback;

public sealed class ListFeedbackQueryValidator : AbstractValidator<ListFeedbackQuery>
{
    public const int MaximumPageSize = 100;

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
        RuleFor(query => query)
            .Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page is outside the supported range.");
    }
}
