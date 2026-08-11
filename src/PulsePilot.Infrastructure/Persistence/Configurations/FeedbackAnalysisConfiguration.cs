using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Configurations;

internal sealed class FeedbackAnalysisConfiguration
    : IEntityTypeConfiguration<FeedbackAnalysis>
{
    public void Configure(EntityTypeBuilder<FeedbackAnalysis> builder)
    {
        builder.ToTable(
            "feedback_analyses",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_analyses_category",
                    "category IN ('Bug', 'FeatureRequest', 'Complaint', 'Question', 'Praise', 'Other')");
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_analyses_component",
                    "component IN ('Payments', 'Authentication', 'Dashboard', 'Reporting', 'Mobile', 'Api', 'Performance', 'General')");
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_analyses_severity",
                    "severity BETWEEN 1 AND 5");
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_analyses_sentiment",
                    "sentiment IN ('Positive', 'Neutral', 'Negative')");
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_analyses_confidence",
                    "confidence BETWEEN 0 AND 1");
            });

        builder.HasKey(analysis => analysis.Id)
            .HasName("pk_feedback_analyses");

        builder.Property(analysis => analysis.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(analysis => analysis.WorkspaceId)
            .HasColumnName("workspace_id")
            .ValueGeneratedNever();

        builder.Property(analysis => analysis.FeedbackId)
            .HasColumnName("feedback_id")
            .ValueGeneratedNever();

        builder.Property(analysis => analysis.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(analysis => analysis.Component)
            .HasColumnName("component")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(analysis => analysis.Severity)
            .HasColumnName("severity")
            .IsRequired();

        builder.Property(analysis => analysis.Sentiment)
            .HasColumnName("sentiment")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(analysis => analysis.Summary)
            .HasColumnName("summary")
            .HasMaxLength(FeedbackAnalysis.MaxSummaryLength)
            .IsRequired();

        builder.Property(analysis => analysis.SuggestedAction)
            .HasColumnName("suggested_action")
            .HasMaxLength(FeedbackAnalysis.MaxSuggestedActionLength)
            .IsRequired();

        builder.Property(analysis => analysis.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(analysis => analysis.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(analysis => analysis.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(analysis => analysis.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_feedback_analyses_workspaces_workspace_id");

        builder.HasOne<FeedbackEntity>()
            .WithMany()
            .HasForeignKey(analysis => new { analysis.WorkspaceId, analysis.FeedbackId })
            .HasPrincipalKey(feedback => new { feedback.WorkspaceId, feedback.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_feedback_analyses_feedback_workspace_id_feedback_id");

        builder.HasIndex(analysis => new { analysis.WorkspaceId, analysis.FeedbackId })
            .IsUnique()
            .HasDatabaseName("ux_feedback_analyses_workspace_id_feedback_id");
    }
}
