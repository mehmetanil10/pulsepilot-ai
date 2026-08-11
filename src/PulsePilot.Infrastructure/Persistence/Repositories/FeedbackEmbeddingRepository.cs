using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PulsePilot.Application.Abstractions.Persistence;
using PulsePilot.Domain.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Repositories;

internal sealed class FeedbackEmbeddingRepository(AppDbContext dbContext)
    : IFeedbackEmbeddingRepository
{
    public Task<FeedbackEmbedding?> GetByFeedbackIdAsync(
        Guid workspaceId,
        Guid feedbackId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.FeedbackEmbeddings.SingleOrDefaultAsync(
            embedding => embedding.WorkspaceId == workspaceId
                && embedding.FeedbackId == feedbackId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<SimilarFeedbackMatch>> FindSimilarAsync(
        Guid workspaceId,
        Guid feedbackId,
        double minimumSimilarity,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (minimumSimilarity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSimilarity),
                "Minimum similarity must be between 0 and 1.");
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be positive.");
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    feedback.id,
                    feedback.title,
                    feedback.content,
                    feedback.source,
                    feedback.created_at,
                    1 - (candidate.embedding <=> source.embedding) AS similarity
                FROM feedback_embeddings AS source
                INNER JOIN feedback_embeddings AS candidate
                    ON candidate.workspace_id = source.workspace_id
                    AND candidate.feedback_id <> source.feedback_id
                INNER JOIN feedback AS feedback
                    ON feedback.workspace_id = candidate.workspace_id
                    AND feedback.id = candidate.feedback_id
                WHERE source.workspace_id = @workspace_id
                    AND source.feedback_id = @feedback_id
                    AND feedback.deleted_at IS NULL
                    AND feedback.processing_status = 'Completed'
                    AND (candidate.embedding <=> source.embedding) <= @maximum_distance
                ORDER BY candidate.embedding <=> source.embedding ASC,
                    feedback.created_at DESC,
                    feedback.id DESC
                LIMIT @limit;
                """;
            AddParameter(command, "workspace_id", workspaceId);
            AddParameter(command, "feedback_id", feedbackId);
            AddParameter(command, "maximum_distance", 1 - minimumSimilarity);
            AddParameter(command, "limit", limit);

            var matches = new List<SimilarFeedbackMatch>(limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add(new SimilarFeedbackMatch(
                    reader.GetGuid(0),
                    reader.IsDBNull(1) ? null : reader.GetString(1),
                    reader.GetString(2),
                    Enum.Parse<FeedbackSource>(reader.GetString(3), ignoreCase: false),
                    reader.GetDouble(5),
                    reader.GetFieldValue<DateTimeOffset>(4)));
            }

            return matches;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task AddAsync(
        FeedbackEmbedding embedding,
        CancellationToken cancellationToken = default)
    {
        await dbContext.FeedbackEmbeddings.AddAsync(embedding, cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
