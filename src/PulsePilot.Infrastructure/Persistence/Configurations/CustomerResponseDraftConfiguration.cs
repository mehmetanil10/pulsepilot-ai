using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.CustomerResponses;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Configurations;

internal sealed class CustomerResponseDraftConfiguration
    : IEntityTypeConfiguration<CustomerResponseDraft>
{
    public void Configure(EntityTypeBuilder<CustomerResponseDraft> builder)
    {
        builder.ToTable("customer_response_drafts");

        builder.HasKey(draft => draft.Id)
            .HasName("pk_customer_response_drafts");

        builder.HasAlternateKey(draft => new { draft.WorkspaceId, draft.Id })
            .HasName("ak_customer_response_drafts_workspace_id_id");

        builder.Property(draft => draft.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(draft => draft.WorkspaceId)
            .HasColumnName("workspace_id")
            .ValueGeneratedNever();

        builder.Property(draft => draft.FeedbackId)
            .HasColumnName("feedback_id")
            .ValueGeneratedNever();

        builder.Property(draft => draft.FeedbackClusterId)
            .HasColumnName("feedback_cluster_id")
            .ValueGeneratedNever();

        builder.Property(draft => draft.SourcePendingActionId)
            .HasColumnName("source_pending_action_id")
            .ValueGeneratedNever();

        builder.Property(draft => draft.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .ValueGeneratedNever();

        builder.Property(draft => draft.Content)
            .HasColumnName("content")
            .HasMaxLength(CustomerResponseDraft.MaxContentLength)
            .IsRequired();

        builder.Property(draft => draft.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(draft => draft.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(draft => draft.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_customer_response_drafts_workspaces_workspace_id");

        builder.HasOne<FeedbackEntity>()
            .WithMany()
            .HasForeignKey(draft => new { draft.WorkspaceId, draft.FeedbackId })
            .HasPrincipalKey(feedback => new { feedback.WorkspaceId, feedback.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_customer_response_drafts_feedback_workspace_id_feedback_id");

        builder.HasOne<FeedbackCluster>()
            .WithMany()
            .HasForeignKey(draft => new { draft.WorkspaceId, draft.FeedbackClusterId })
            .HasPrincipalKey(cluster => new { cluster.WorkspaceId, cluster.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_customer_response_drafts_clusters_workspace_id_cluster_id");

        builder.HasOne<PendingAction>()
            .WithMany()
            .HasForeignKey(draft => new { draft.WorkspaceId, draft.SourcePendingActionId })
            .HasPrincipalKey(action => new { action.WorkspaceId, action.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_customer_response_drafts_actions_workspace_id_source_action_id");

        builder.HasOne<WorkspaceMember>()
            .WithMany()
            .HasForeignKey(draft => new { draft.WorkspaceId, draft.CreatedByUserId })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName(
                "fk_customer_response_drafts_members_workspace_id_created_by_user_id");

        builder.HasIndex(draft => new { draft.WorkspaceId, draft.FeedbackId })
            .HasDatabaseName("ix_customer_response_drafts_workspace_id_feedback_id");

        builder.HasIndex(draft => new { draft.WorkspaceId, draft.FeedbackClusterId })
            .HasDatabaseName("ix_customer_response_drafts_workspace_id_cluster_id");

        builder.HasIndex(draft => new { draft.WorkspaceId, draft.CreatedByUserId })
            .HasDatabaseName("ix_customer_response_drafts_workspace_id_created_by_user_id");

        builder.HasIndex(draft => new { draft.WorkspaceId, draft.SourcePendingActionId })
            .IsUnique()
            .HasDatabaseName("ux_customer_response_drafts_workspace_id_source_action_id");
    }
}
