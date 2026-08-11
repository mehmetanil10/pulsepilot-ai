using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable(
            "workspace_members",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_workspace_members_role",
                "role IN ('Admin', 'Member')"));

        builder.HasKey(member => new { member.WorkspaceId, member.UserId })
            .HasName("pk_workspace_members");

        builder.Property(member => member.WorkspaceId)
            .HasColumnName("workspace_id")
            .ValueGeneratedNever();

        builder.Property(member => member.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();

        builder.Property(member => member.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(member => member.JoinedAt)
            .HasColumnName("joined_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(member => member.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_workspace_members_workspaces_workspace_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_workspace_members_users_user_id");

        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("ix_workspace_members_user_id");
    }
}
