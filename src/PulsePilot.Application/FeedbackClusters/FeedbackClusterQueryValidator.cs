using FluentValidation;

namespace PulsePilot.Application.FeedbackClusters;

public sealed class FeedbackClusterQueryValidator : AbstractValidator<FeedbackClusterQuery>
{
    public const int MaximumPageSize = 100;

    public FeedbackClusterQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaximumPageSize);
        RuleFor(query => query)
            .Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page is outside the supported range.");
    }
}
