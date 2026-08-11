using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PulsePilot.Domain.Workspaces;

namespace PulsePilot.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces");

        builder.HasKey(workspace => workspace.Id)
            .HasName("pk_workspaces");

        builder.Property(workspace => workspace.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(workspace => workspace.Name)
            .HasColumnName("name")
            .HasMaxLength(Workspace.MaxNameLength)
            .IsRequired();

        builder.Property(workspace => workspace.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(workspace => workspace.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
