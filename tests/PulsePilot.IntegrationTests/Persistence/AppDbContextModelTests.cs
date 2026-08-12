using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;
using PulsePilot.Infrastructure.Persistence;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.IntegrationTests.Persistence;

public sealed class AppDbContextModelTests
{
    [Fact]
    public void Model_MapsEntitiesToExpectedTables()
    {
        using var dbContext = CreateDbContext();

        Assert.Equal("users", dbContext.Model.FindEntityType(typeof(User))?.GetTableName());
        Assert.Equal("workspaces", dbContext.Model.FindEntityType(typeof(Workspace))?.GetTableName());
        Assert.Equal(
            "workspace_members",
            dbContext.Model.FindEntityType(typeof(WorkspaceMember))?.GetTableName());
        Assert.Equal("feedback", dbContext.Model.FindEntityType(typeof(FeedbackEntity))?.GetTableName());
        Assert.Equal(
            "feedback_analyses",
            dbContext.Model.FindEntityType(typeof(FeedbackAnalysis))?.GetTableName());
        Assert.Equal(
            "feedback_embeddings",
            dbContext.Model.FindEntityType(typeof(FeedbackEmbedding))?.GetTableName());
        Assert.Equal(
            "feedback_clusters",
            dbContext.Model.FindEntityType(typeof(FeedbackCluster))?.GetTableName());
    }

    [Fact]
    public void User_NormalizedEmailIndex_IsUnique()
    {
        using var dbContext = CreateDbContext();
        var userEntity = dbContext.Model.FindEntityType(typeof(User));
        var normalizedEmail = userEntity?.FindProperty(nameof(User.NormalizedEmail));

        var index = Assert.Single(
            userEntity!.GetIndexes(),
            candidate => candidate.Properties.Count == 1
                && candidate.Properties[0] == normalizedEmail);

        Assert.True(index.IsUnique);
        Assert.Equal("ux_users_normalized_email", index.GetDatabaseName());
    }

    [Fact]
    public void WorkspaceMember_UsesCompositePrimaryKeyAndStringRole()
    {
        using var dbContext = CreateDbContext();
        var memberEntity = dbContext.Model.FindEntityType(typeof(WorkspaceMember));
        var primaryKey = memberEntity?.FindPrimaryKey();
        var role = memberEntity?.FindProperty(nameof(WorkspaceMember.Role));

        Assert.Equal(
            [nameof(WorkspaceMember.WorkspaceId), nameof(WorkspaceMember.UserId)],
            primaryKey!.Properties.Select(property => property.Name));
        Assert.Equal(typeof(string), role!.GetTypeMapping().Converter?.ProviderClrType);
    }

    [Fact]
    [Obsolete]
    public void Feedback_UsesSoftDeleteFilterAndRestrictiveForeignKeys()
    {
        using var dbContext = CreateDbContext();
        var feedbackEntity = dbContext.Model.FindEntityType(typeof(FeedbackEntity));

        Assert.NotNull(feedbackEntity?.GetQueryFilter());
        Assert.All(
            feedbackEntity!.GetForeignKeys(),
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void Feedback_UsesStringEnumsAndWorkspaceIndexes()
    {
        using var dbContext = CreateDbContext();
        var feedbackEntity = dbContext.Model.FindEntityType(typeof(FeedbackEntity));
        var source = feedbackEntity?.FindProperty(nameof(FeedbackEntity.Source));
        var processingStatus = feedbackEntity?.FindProperty(nameof(FeedbackEntity.ProcessingStatus));
        var processingLeaseId = feedbackEntity?.FindProperty(
            nameof(FeedbackEntity.ProcessingLeaseId));
        var processingStartedAt = feedbackEntity?.FindProperty(
            nameof(FeedbackEntity.ProcessingStartedAt));
        var feedbackClusterId = feedbackEntity?.FindProperty(
            nameof(FeedbackEntity.FeedbackClusterId));
        var indexNames = feedbackEntity!
            .GetIndexes()
            .Select(index => index.GetDatabaseName())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(typeof(string), source!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(typeof(string), processingStatus!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal("processing_lease_id", processingLeaseId!.GetColumnName());
        Assert.Equal("processing_started_at", processingStartedAt!.GetColumnName());
        Assert.Equal("feedback_cluster_id", feedbackClusterId!.GetColumnName());
        Assert.Contains("ix_feedback_workspace_id_created_at", indexNames);
        Assert.Contains("ix_feedback_workspace_id_processing_status", indexNames);
        Assert.Contains("ix_feedback_processing_status_started_at", indexNames);
        Assert.Contains("ix_feedback_workspace_id_cluster_id", indexNames);
    }

    [Fact]
    public void FeedbackAnalysis_UsesStructuredTypesAndWorkspaceScopedUniqueIndex()
    {
        using var dbContext = CreateDbContext();
        var analysisEntity = dbContext.Model.FindEntityType(typeof(FeedbackAnalysis));
        var category = analysisEntity?.FindProperty(nameof(FeedbackAnalysis.Category));
        var component = analysisEntity?.FindProperty(nameof(FeedbackAnalysis.Component));
        var sentiment = analysisEntity?.FindProperty(nameof(FeedbackAnalysis.Sentiment));
        var confidence = analysisEntity?.FindProperty(nameof(FeedbackAnalysis.Confidence));
        var uniqueIndex = Assert.Single(
            analysisEntity!.GetIndexes(),
            index => index.IsUnique);

        Assert.Equal(typeof(string), category!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(typeof(string), component!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(typeof(string), sentiment!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(5, confidence!.GetPrecision());
        Assert.Equal(4, confidence.GetScale());
        Assert.Equal(
            [nameof(FeedbackAnalysis.WorkspaceId), nameof(FeedbackAnalysis.FeedbackId)],
            uniqueIndex.Properties.Select(property => property.Name));
        Assert.Equal(
            "ux_feedback_analyses_workspace_id_feedback_id",
            uniqueIndex.GetDatabaseName());
    }

    [Fact]
    public void FeedbackEmbedding_UsesPgvectorAndWorkspaceScopedIndexes()
    {
        using var dbContext = CreateDbContext();
        var embeddingEntity = dbContext.Model.FindEntityType(typeof(FeedbackEmbedding));
        var vector = embeddingEntity?.FindProperty("_values");
        var uniqueIndex = Assert.Single(
            embeddingEntity!.GetIndexes(),
            index => index.IsUnique);
        var vectorIndex = Assert.Single(
            embeddingEntity.GetIndexes(),
            index => index.GetDatabaseName() == "ix_feedback_embeddings_embedding_cosine");

        Assert.Equal("vector(1536)", vector!.GetColumnType());
        Assert.Equal("hnsw", vectorIndex.GetMethod());
        Assert.Equal(
            [nameof(FeedbackEmbedding.WorkspaceId), nameof(FeedbackEmbedding.FeedbackId)],
            uniqueIndex.Properties.Select(property => property.Name));
    }

    [Fact]
    public void FeedbackCluster_UsesStructuredClassificationAndTenantScopedKey()
    {
        using var dbContext = CreateDbContext();
        var clusterEntity = dbContext.Model.FindEntityType(typeof(FeedbackCluster));
        var category = clusterEntity?.FindProperty(nameof(FeedbackCluster.Category));
        var component = clusterEntity?.FindProperty(nameof(FeedbackCluster.Component));
        var priorityScore = clusterEntity?.FindProperty(nameof(FeedbackCluster.PriorityScore));
        var priority = clusterEntity?.FindProperty(nameof(FeedbackCluster.Priority));
        var alternateKey = Assert.Single(
            clusterEntity!.GetKeys(),
            key => !key.IsPrimaryKey());

        Assert.Equal(typeof(string), category!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(typeof(string), component!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(5, priorityScore!.GetPrecision());
        Assert.Equal(2, priorityScore.GetScale());
        Assert.Equal(typeof(string), priority!.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(
            [nameof(FeedbackCluster.WorkspaceId), nameof(FeedbackCluster.Id)],
            alternateKey.Properties.Select(property => property.Name));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=pulsepilot;Username=pulsepilot",
                options => options.UseVector())
            .Options;

        return new AppDbContext(options);
    }
}
