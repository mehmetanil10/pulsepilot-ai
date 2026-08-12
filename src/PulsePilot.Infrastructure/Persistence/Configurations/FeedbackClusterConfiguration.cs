using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Infrastructure.Persistence.Configurations;

internal sealed class FeedbackClusterConfiguration
    : IEntityTypeConfiguration<FeedbackCluster>
{
    public void Configure(EntityTypeBuilder<FeedbackCluster> builder)
    {
        builder.ToTable(
            "feedback_clusters",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_clusters_category",
                    "category IN ('Bug', 'FeatureRequest', 'Complaint', 'Question', 'Praise', 'Other')");
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_clusters_component",
                    "component IN ('Payments', 'Authentication', 'Dashboard', 'Reporting', 'Mobile', 'Api', 'Performance', 'General')");
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_clusters_priority_score",
                    "priority_score BETWEEN 0 AND 100");
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_clusters_priority",
                    "priority IN ('P1', 'P2', 'P3', 'P4')");
            });

        builder.HasKey(cluster => cluster.Id)
            .HasName("pk_feedback_clusters");

        builder.HasAlternateKey(cluster => new { cluster.WorkspaceId, cluster.Id })
            .HasName("ak_feedback_clusters_workspace_id_id");

        builder.Property(cluster => cluster.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(cluster => cluster.WorkspaceId)
            .HasColumnName("workspace_id")
            .ValueGeneratedNever();

        builder.Property(cluster => cluster.Title)
            .HasColumnName("title")
            .HasMaxLength(FeedbackCluster.MaxTitleLength)
            .IsRequired();

        builder.Property(cluster => cluster.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(cluster => cluster.Component)
            .HasColumnName("component")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(cluster => cluster.PriorityScore)
            .HasColumnName("priority_score")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(cluster => cluster.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(cluster => cluster.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(cluster => cluster.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(cluster => cluster.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_feedback_clusters_workspaces_workspace_id");

        builder.HasIndex(cluster => new { cluster.WorkspaceId, cluster.UpdatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_feedback_clusters_workspace_id_updated_at");

        builder.HasIndex(cluster => new { cluster.WorkspaceId, cluster.PriorityScore })
            .IsDescending(false, true)
            .HasDatabaseName("ix_feedback_clusters_workspace_id_priority_score");

        builder.HasIndex(cluster => new
        {
            cluster.WorkspaceId,
            cluster.Category,
            cluster.Component,
        })
            .HasDatabaseName("ix_feedback_clusters_workspace_category_component");
    }
}
