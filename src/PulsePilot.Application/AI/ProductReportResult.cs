namespace PulsePilot.Application.AI;

public sealed record ProductReportResult(
    string Title,
    string ExecutiveSummary,
    IReadOnlyList<string> KeyInsights,
    IReadOnlyList<string> RecommendedEngineeringPriorities)
{
    public const int MaxTitleLength = 200;
    public const int MaxExecutiveSummaryLength = 1_000;
    public const int MaxListItemLength = 200;
    public const int MaxListItemCount = 5;

    public ProductReportResult Normalize()
    {
        return this with
        {
            Title = Title.Trim(),
            ExecutiveSummary = ExecutiveSummary.Trim(),
            KeyInsights = KeyInsights.Select(item => item.Trim()).ToList(),
            RecommendedEngineeringPriorities = RecommendedEngineeringPriorities
                .Select(item => item.Trim())
                .ToList(),
        };
    }
}
