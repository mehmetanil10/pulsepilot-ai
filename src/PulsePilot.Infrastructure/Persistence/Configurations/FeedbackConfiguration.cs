using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PulsePilot.Domain.Users;
using PulsePilot.Domain.Workspaces;

using FeedbackEntity = PulsePilot.Domain.Feedback.Feedback;

namespace PulsePilot.Infrastructure.Persistence.Configurations;

internal sealed class FeedbackConfiguration : IEntityTypeConfiguration<FeedbackEntity>
{
    public void Configure(EntityTypeBuilder<FeedbackEntity> builder)
    {
        builder.ToTable(
            "feedback",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_source",
                    "source IN ('Manual', 'Email', 'Support', 'Survey', 'Api', 'AppReview')");
                tableBuilder.HasCheckConstraint(
                    "ck_feedback_processing_status",
                    "processing_status IN ('Pending', 'Processing', 'Completed', 'Failed')");
            });

        builder.HasKey(feedback => feedback.Id)
            .HasName("pk_feedback");

        builder.HasAlternateKey(feedback => new { feedback.WorkspaceId, feedback.Id })
            .HasName("ak_feedback_workspace_id_id");

        builder.Property(feedback => feedback.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(feedback => feedback.WorkspaceId)
            .HasColumnName("workspace_id")
            .ValueGeneratedNever();

        builder.Property(feedback => feedback.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .ValueGeneratedNever();

        builder.Property(feedback => feedback.Title)
            .HasColumnName("title")
            .HasMaxLength(FeedbackEntity.MaxTitleLength);

        builder.Property(feedback => feedback.Content)
            .HasColumnName("content")
            .HasMaxLength(FeedbackEntity.MaxContentLength)
            .IsRequired();

        builder.Property(feedback => feedback.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(feedback => feedback.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(FeedbackEntity.MaxCustomerNameLength);

        builder.Property(feedback => feedback.CustomerEmail)
            .HasColumnName("customer_email")
            .HasMaxLength(FeedbackEntity.MaxCustomerEmailLength);

        builder.Property(feedback => feedback.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(feedback => feedback.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(feedback => feedback.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(feedback => feedback.DeletedAt)
            .HasColumnName("deleted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasOne<Workspace>()
            .WithMany()
            .HasForeignKey(feedback => feedback.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_feedback_workspaces_workspace_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(feedback => feedback.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_feedback_users_created_by_user_id");

        builder.HasIndex(feedback => new { feedback.WorkspaceId, feedback.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_feedback_workspace_id_created_at");

        builder.HasIndex(feedback => new { feedback.WorkspaceId, feedback.ProcessingStatus })
            .HasDatabaseName("ix_feedback_workspace_id_processing_status");

        builder.HasIndex(feedback => feedback.CreatedByUserId)
            .HasDatabaseName("ix_feedback_created_by_user_id");

        builder.HasQueryFilter(feedback => feedback.DeletedAt == null);
    }
}
