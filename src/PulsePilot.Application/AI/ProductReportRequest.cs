namespace PulsePilot.Application.AI;

public sealed record ProductReportRequest(
    DateTimeOffset FromInclusive,
    DateTimeOffset ToExclusive,
    int PeriodDays,
    int TotalFeedbackCount,
    int AnalyzedFeedbackCount,
    decimal? AverageSeverity,
    IReadOnlyList<ProductReportBreakdownItem> Categories,
    IReadOnlyList<ProductReportBreakdownItem> Components,
    IReadOnlyList<ProductReportBreakdownItem> Sentiments,
    IReadOnlyList<ProductReportTrendingIssue> TrendingIssues);

public sealed record ProductReportBreakdownItem(
    string Name,
    int Count);

public sealed record ProductReportTrendingIssue(
    string Category,
    string Component,
    string Priority,
    decimal PriorityScore,
    int CurrentPeriodCount,
    int PreviousPeriodCount,
    int DeltaCount,
    decimal? GrowthPercentage,
    bool IsNew);
