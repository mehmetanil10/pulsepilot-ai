using FluentValidation;

namespace PulsePilot.Application.AI;

public sealed class ProductReportResultValidator : AbstractValidator<ProductReportResult>
{
    public ProductReportResultValidator()
    {
        RuleFor(result => result.Title)
            .NotEmpty()
            .MaximumLength(ProductReportResult.MaxTitleLength);
        RuleFor(result => result.ExecutiveSummary)
            .NotEmpty()
            .MaximumLength(ProductReportResult.MaxExecutiveSummaryLength);
        RuleFor(result => result.KeyInsights)
            .NotNull()
            .Must(HaveAcceptedItemCount)
            .WithMessage(
                $"KeyInsights must contain between 1 and {ProductReportResult.MaxListItemCount} items.")
            .Must(HaveDistinctItems)
            .WithMessage("KeyInsights cannot contain duplicate items.");
        RuleForEach(result => result.KeyInsights)
            .NotEmpty()
            .MaximumLength(ProductReportResult.MaxListItemLength);
        RuleFor(result => result.RecommendedEngineeringPriorities)
            .NotNull()
            .Must(HaveAcceptedItemCount)
            .WithMessage(
                $"RecommendedEngineeringPriorities must contain between 1 and {ProductReportResult.MaxListItemCount} items.")
            .Must(HaveDistinctItems)
            .WithMessage("RecommendedEngineeringPriorities cannot contain duplicate items.");
        RuleForEach(result => result.RecommendedEngineeringPriorities)
            .NotEmpty()
            .MaximumLength(ProductReportResult.MaxListItemLength);
    }

    private static bool HaveAcceptedItemCount(IReadOnlyList<string>? items)
    {
        return items is not null
            && items.Count is >= 1 and <= ProductReportResult.MaxListItemCount;
    }

    private static bool HaveDistinctItems(IReadOnlyList<string>? items)
    {
        return items is not null
            && items
                .Select(item => item?.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == items.Count;
    }
}
