using FluentValidation;

namespace PulsePilot.Application.Backlog;

public sealed class BacklogItemQueryValidator : AbstractValidator<BacklogItemQuery>
{
    public const int MaximumPageSize = 100;

    public BacklogItemQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaximumPageSize);
        RuleFor(query => query.Status)
            .IsInEnum()
            .When(query => query.Status.HasValue);
        RuleFor(query => query.Priority)
            .IsInEnum()
            .When(query => query.Priority.HasValue);
        RuleFor(query => query.SourcePendingActionId)
            .NotEqual(Guid.Empty)
            .When(query => query.SourcePendingActionId.HasValue);
        RuleFor(query => query)
            .Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page is outside the supported range.");
    }
}
