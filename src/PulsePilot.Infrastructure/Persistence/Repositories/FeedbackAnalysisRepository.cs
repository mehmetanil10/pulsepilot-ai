using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class FeedbackAnalysisRepository(AppDbContext dbContext)
    : IFeedbackAnalysisRepository
{
    public Task<FeedbackAnalysis?> GetByFeedbackIdAsync(
        Guid workspaceId,
        Guid feedbackId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FeedbackAnalyses.SingleOrDefaultAsync(
            analysis => analysis.WorkspaceId == workspaceId
                && analysis.FeedbackId == feedbackId,
            cancellationToken);
    }

    public async Task AddAsync(
        FeedbackAnalysis analysis,
        CancellationToken cancellationToken = default)
    {
        await dbContext.FeedbackAnalyses.AddAsync(analysis, cancellationToken);
    }
}
