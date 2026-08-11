using FluentValidation;
using Microsoft.Extensions.Options;

namespace PulsePilot.Application.Feedback;

public sealed class SimilarFeedbackQueryValidator : AbstractValidator<SimilarFeedbackQuery>
{
    public SimilarFeedbackQueryValidator(IOptions<SemanticSearchOptions> options)
    {
        var maxLimit = options.Value.MaxLimit;

        RuleFor(query => query.Limit)
            .InclusiveBetween(1, maxLimit)
            .When(query => query.Limit.HasValue);
    }
}
