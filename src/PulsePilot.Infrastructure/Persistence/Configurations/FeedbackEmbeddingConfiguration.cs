using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Configurations;

internal sealed class FeedbackEmbeddingConfiguration
    : IEntityTypeConfiguration<FeedbackEmbedding>
{
    public void Configure(EntityTypeBuilder<FeedbackEmbedding> builder)
    {
        builder.ToTable("feedback_embeddings");

        builder.HasKey(embedding => embedding.Id)
            .HasName("pk_feedback_embeddings");

        builder.Property(embedding => embedding.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(embedding => embedding.WorkspaceId)
            .HasColumnName("workspace_id")
            .ValueGeneratedNever();

        builder.Property(embedding => embedding.FeedbackId)
            .HasColumnName("feedback_id")
            .ValueGeneratedNever();

        builder.Ignore(embedding => embedding.Values);

        var vectorProperty = builder.Property<float[]>("_values")
            .HasColumnName("embedding")
            .HasColumnType($"vector({FeedbackEmbedding.Dimensions})")
            .HasConversion(
                values => new Vector(values),
                vector => vector.ToArray())
            .IsRequired();
        vectorProperty.Metadata.SetValueComparer(new ValueComparer<float[]>(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            values => values.Aggregate(0, HashCode.Combine),
            values => values.ToArray()));

        builder.Property(embedding => embedding.Model)
            .HasColumnName("model")
            .HasMaxLength(FeedbackEmbedding.MaxModelLength)
            .IsRequired();

        builder.Property(embedding => embedding.SourceHash)
            .HasColumnName("source_hash")
            .HasMaxLength(FeedbackEmbedding.SourceHashLength)
            .IsFixedLength()
            .IsRequired();

        builder.Property(embedding => embedding.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(embedding => embedding.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(embedding => embedding.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_feedback_embeddings_workspaces_workspace_id");

        builder.HasOne<FeedbackEntity>()
            .WithMany()
            .HasForeignKey(embedding => new { embedding.WorkspaceId, embedding.FeedbackId })
            .HasPrincipalKey(feedback => new { feedback.WorkspaceId, feedback.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_feedback_embeddings_feedback_workspace_id_feedback_id");

        builder.HasIndex(embedding => new { embedding.WorkspaceId, embedding.FeedbackId })
            .IsUnique()
            .HasDatabaseName("ux_feedback_embeddings_workspace_id_feedback_id");

        builder.HasIndex("_values")
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasStorageParameter("m", 16)
            .HasStorageParameter("ef_construction", 64)
            .HasDatabaseName("ix_feedback_embeddings_embedding_cosine");
    }
}
