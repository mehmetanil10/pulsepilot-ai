using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class FeedbackListRepository(AppDbContext dbContext)
    : IFeedbackListRepository
{
    public async Task<FeedbackListPageData> GetPageAsync(
        Guid workspaceId,
        FeedbackListFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));
        }

        if (skip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(skip));
        }

        if (take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(take));
        }

        var query = BuildFilteredQuery(workspaceId, filter);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await (
            from feedback in query
            join analysis in dbContext.FeedbackAnalyses.AsNoTracking()
                on new { feedback.WorkspaceId, FeedbackId = feedback.Id }
                equals new { analysis.WorkspaceId, analysis.FeedbackId }
                into analyses
            from analysis in analyses.DefaultIfEmpty()
            orderby feedback.CreatedAt descending, feedback.Id descending
            select new FeedbackListItemData(
                feedback.Id,
                feedback.FeedbackClusterId,
                feedback.Title,
                feedback.Content,
                feedback.Source,
                feedback.ProcessingStatus,
                feedback.CreatedAt,
                feedback.UpdatedAt,
                analysis == null ? null : analysis.Category,
                analysis == null ? null : analysis.Component,
                analysis == null ? null : analysis.Severity,
                analysis == null ? null : analysis.Sentiment))
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new FeedbackListPageData(items, totalCount);
    }

    private IQueryable<FeedbackEntity> BuildFilteredQuery(
        Guid workspaceId,
        FeedbackListFilter filter)
    {
        var query = dbContext.Feedback
            .AsNoTracking()
            .Where(feedback => feedback.WorkspaceId == workspaceId);

        if (filter.Source.HasValue)
        {
            query = query.Where(feedback => feedback.Source == filter.Source.Value);
        }

        if (filter.ProcessingStatus.HasValue)
        {
            query = query.Where(
                feedback => feedback.ProcessingStatus == filter.ProcessingStatus.Value);
        }

        if (filter.CreatedFromInclusive.HasValue)
        {
            query = query.Where(
                feedback => feedback.CreatedAt >= filter.CreatedFromInclusive.Value);
        }

        if (filter.CreatedToExclusive.HasValue)
        {
            query = query.Where(
                feedback => feedback.CreatedAt < filter.CreatedToExclusive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{EscapeLikePattern(filter.Search.Trim())}%";
            query = query.Where(feedback =>
                EF.Functions.ILike(feedback.Title ?? string.Empty, pattern, "\\")
                || EF.Functions.ILike(feedback.Content, pattern, "\\"));
        }

        if (filter.Category.HasValue
            || filter.Component.HasValue
            || filter.Severity.HasValue
            || filter.Sentiment.HasValue)
        {
            query = query.Where(feedback => dbContext.FeedbackAnalyses.Any(analysis =>
                analysis.WorkspaceId == workspaceId
                && analysis.FeedbackId == feedback.Id
                && (!filter.Category.HasValue || analysis.Category == filter.Category.Value)
                && (!filter.Component.HasValue || analysis.Component == filter.Component.Value)
                && (!filter.Severity.HasValue || analysis.Severity == filter.Severity.Value)
                && (!filter.Sentiment.HasValue || analysis.Sentiment == filter.Sentiment.Value)));
        }

        return query;
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
