using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PulsePilot.Domain.Actions;
using PulsePilot.Domain.Backlog;
using PulsePilot.Domain.Feedback;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Infrastructure.Persistence.Configurations;

internal sealed class BacklogItemConfiguration : IEntityTypeConfiguration<BacklogItem>
{
    public void Configure(EntityTypeBuilder<BacklogItem> builder)
    {
        builder.ToTable(
            "backlog_items",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_backlog_items_priority",
                    "priority IN ('P1', 'P2', 'P3', 'P4')");
                tableBuilder.HasCheckConstraint(
                    "ck_backlog_items_status",
                    "status IN ('Open', 'InProgress', 'Resolved', 'Closed')");
            });

        builder.HasKey(backlogItem => backlogItem.Id)
            .HasName("pk_backlog_items");

        builder.HasAlternateKey(backlogItem => new
        {
            backlogItem.WorkspaceId,
            backlogItem.Id,
        })
            .HasName("ak_backlog_items_workspace_id_id");

        builder.Property(backlogItem => backlogItem.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(backlogItem => backlogItem.WorkspaceId)
            .HasColumnName("workspace_id")
            .ValueGeneratedNever();

        builder.Property(backlogItem => backlogItem.SourceClusterId)
            .HasColumnName("source_cluster_id")
            .ValueGeneratedNever();

        builder.Property(backlogItem => backlogItem.SourcePendingActionId)
            .HasColumnName("source_pending_action_id")
            .ValueGeneratedNever();

        builder.Property(backlogItem => backlogItem.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .ValueGeneratedNever();

        builder.Property(backlogItem => backlogItem.Title)
            .HasColumnName("title")
            .HasMaxLength(BacklogItem.MaxTitleLength)
            .IsRequired();

        builder.Property(backlogItem => backlogItem.Description)
            .HasColumnName("description")
            .HasMaxLength(BacklogItem.MaxDescriptionLength)
            .IsRequired();

        builder.Property(backlogItem => backlogItem.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(backlogItem => backlogItem.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(backlogItem => backlogItem.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(backlogItem => backlogItem.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(backlogItem => backlogItem.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_backlog_items_workspaces_workspace_id");

        builder.HasOne<FeedbackCluster>()
            .WithMany()
            .HasForeignKey(backlogItem => new
            {
                backlogItem.WorkspaceId,
                backlogItem.SourceClusterId,
            })
            .HasPrincipalKey(cluster => new { cluster.WorkspaceId, cluster.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_backlog_items_clusters_workspace_id_source_cluster_id");

        builder.HasOne<PendingAction>()
            .WithMany()
            .HasForeignKey(backlogItem => new
            {
                backlogItem.WorkspaceId,
                backlogItem.SourcePendingActionId,
            })
            .HasPrincipalKey(action => new { action.WorkspaceId, action.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_backlog_items_actions_workspace_id_source_action_id");

        builder.HasOne<WorkspaceMember>()
            .WithMany()
            .HasForeignKey(backlogItem => new
            {
                backlogItem.WorkspaceId,
                backlogItem.CreatedByUserId,
            })
            .HasPrincipalKey(member => new { member.WorkspaceId, member.UserId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_backlog_items_members_workspace_id_created_by_user_id");

        builder.HasIndex(backlogItem => new
        {
            backlogItem.WorkspaceId,
            backlogItem.CreatedByUserId,
        })
            .HasDatabaseName("ix_backlog_items_workspace_id_created_by_user_id");

        builder.HasIndex(backlogItem => new
        {
            backlogItem.WorkspaceId,
            backlogItem.SourceClusterId,
        })
            .IsUnique()
            .HasFilter("status IN ('Open', 'InProgress')")
            .HasDatabaseName("ux_backlog_items_active_source_cluster");

        builder.HasIndex(backlogItem => new
        {
            backlogItem.WorkspaceId,
            backlogItem.SourcePendingActionId,
        })
            .IsUnique()
            .HasDatabaseName("ux_backlog_items_workspace_id_source_action_id");

        builder.HasIndex(backlogItem => new
        {
            backlogItem.WorkspaceId,
            backlogItem.Status,
            backlogItem.Priority,
            backlogItem.CreatedAt,
        })
            .IsDescending(false, false, false, true)
            .HasDatabaseName("ix_backlog_items_workspace_status_priority_created_at");
    }
}
