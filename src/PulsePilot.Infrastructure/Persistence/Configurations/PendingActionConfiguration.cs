using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Configurations;

internal sealed class PendingActionConfiguration : IEntityTypeConfiguration<PendingAction>
{
    public void Configure(EntityTypeBuilder<PendingAction> builder)
    {
        builder.ToTable(
            "pending_actions",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_pending_actions_action_type",
                    "action_type IN ('CreateEngineeringIssue', 'DraftCustomerResponse', 'GenerateReport', 'EscalateIssue')");
                tableBuilder.HasCheckConstraint(
                    "ck_pending_actions_status",
                    "status IN ('Pending', 'Approved', 'Rejected', 'Executed', 'Failed')");
                tableBuilder.HasCheckConstraint(
                    "ck_pending_actions_payload_object",
                    "jsonb_typeof(payload) = 'object'");
                tableBuilder.HasCheckConstraint(
                    "ck_pending_actions_review_state",
                    "(status = 'Pending' AND approved_at IS NULL AND rejected_at IS NULL) "
                    + "OR (status = 'Approved' AND approved_at IS NOT NULL AND rejected_at IS NULL) "
                    + "OR (status = 'Rejected' AND approved_at IS NULL AND rejected_at IS NOT NULL) "
                    + "OR (status IN ('Executed', 'Failed') AND approved_at IS NOT NULL AND rejected_at IS NULL)");
                tableBuilder.HasCheckConstraint(
                    "ck_pending_actions_execution_state",
                    "(status = 'Executed' AND executed_at IS NOT NULL) "
                    + "OR (status <> 'Executed' AND executed_at IS NULL)");
            });

        builder.HasKey(pendingAction => pendingAction.Id)
            .HasName("pk_pending_actions");

        builder.HasAlternateKey(pendingAction => new
        {
            pendingAction.WorkspaceId,
            pendingAction.Id,
        })
            .HasName("ak_pending_actions_workspace_id_id");

        builder.Property(pendingAction => pendingAction.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(pendingAction => pendingAction.WorkspaceId)
            .HasColumnName("workspace_id")
            .ValueGeneratedNever();

        builder.Property(pendingAction => pendingAction.FeedbackId)
            .HasColumnName("feedback_id")
            .ValueGeneratedNever();

        builder.Property(pendingAction => pendingAction.FeedbackClusterId)
            .HasColumnName("feedback_cluster_id")
            .ValueGeneratedNever();

        builder.Property(pendingAction => pendingAction.ActionType)
            .HasColumnName("action_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(pendingAction => pendingAction.Title)
            .HasColumnName("title")
            .HasMaxLength(PendingAction.MaxTitleLength)
            .IsRequired();

        builder.Property(pendingAction => pendingAction.Description)
            .HasColumnName("description")
            .HasMaxLength(PendingAction.MaxDescriptionLength)
            .IsRequired();

        builder.Property(pendingAction => pendingAction.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb")
            .HasMaxLength(PendingAction.MaxPayloadLength)
            .IsRequired();

        builder.Property(pendingAction => pendingAction.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(pendingAction => pendingAction.ApprovedAt)
            .HasColumnName("approved_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(pendingAction => pendingAction.RejectedAt)
            .HasColumnName("rejected_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(pendingAction => pendingAction.ExecutedAt)
            .HasColumnName("executed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(pendingAction => pendingAction.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(pendingAction => pendingAction.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(pendingAction => pendingAction.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pending_actions_workspaces_workspace_id");

        builder.HasOne<FeedbackEntity>()
            .WithMany()
            .HasForeignKey(pendingAction => new
            {
                pendingAction.WorkspaceId,
                pendingAction.FeedbackId,
            })
            .HasPrincipalKey(feedback => new { feedback.WorkspaceId, feedback.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pending_actions_feedback_workspace_id_feedback_id");

        builder.HasOne<FeedbackCluster>()
            .WithMany()
            .HasForeignKey(pendingAction => new
            {
                pendingAction.WorkspaceId,
                pendingAction.FeedbackClusterId,
            })
            .HasPrincipalKey(cluster => new { cluster.WorkspaceId, cluster.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pending_actions_clusters_workspace_id_cluster_id");

        builder.HasIndex(pendingAction => new
        {
            pendingAction.WorkspaceId,
            pendingAction.FeedbackId,
        })
            .HasDatabaseName("ix_pending_actions_workspace_id_feedback_id");

        builder.HasIndex(pendingAction => new
        {
            pendingAction.WorkspaceId,
            pendingAction.Status,
            pendingAction.CreatedAt,
        })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_pending_actions_workspace_status_created_at");

        builder.HasIndex(pendingAction => new
        {
            pendingAction.WorkspaceId,
            pendingAction.FeedbackClusterId,
            pendingAction.ActionType,
        })
            .IsUnique()
            .HasFilter("status IN ('Pending', 'Approved')")
            .HasDatabaseName("ux_pending_actions_active_cluster_action_type");
    }
}
