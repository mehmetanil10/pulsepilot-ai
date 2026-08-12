using FluentValidation;

namespace PulsePilot.Application.Actions;

public sealed class PendingActionQueryValidator : AbstractValidator<PendingActionQuery>
{
    public const int MaximumPageSize = 100;

    public PendingActionQueryValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThan(0);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, MaximumPageSize);
        RuleFor(query => query.Status)
            .IsInEnum()
            .When(query => query.Status.HasValue);
        RuleFor(query => query)
            .Must(query => (long)(query.Page - 1) * query.PageSize <= int.MaxValue)
            .WithMessage("The requested page is outside the supported range.");
    }
}
