using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulsePilot.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFeedbackProcessingLease : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "processing_lease_id",
            table: "feedback",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "processing_started_at",
            table: "feedback",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE feedback
            SET processing_status = 'Pending'
            WHERE processing_status = 'Processing'
            """);

        migrationBuilder.CreateIndex(
            name: "ix_feedback_processing_status_started_at",
            table: "feedback",
            columns: new[] { "processing_status", "processing_started_at" });

        migrationBuilder.AddCheckConstraint(
            name: "ck_feedback_processing_lease",
            table: "feedback",
            sql: "(processing_status = 'Processing' AND processing_lease_id IS NOT NULL AND processing_started_at IS NOT NULL) OR (processing_status <> 'Processing' AND processing_lease_id IS NULL AND processing_started_at IS NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_feedback_processing_status_started_at",
            table: "feedback");

        migrationBuilder.DropCheckConstraint(
            name: "ck_feedback_processing_lease",
            table: "feedback");

        migrationBuilder.DropColumn(
            name: "processing_lease_id",
            table: "feedback");

        migrationBuilder.DropColumn(
            name: "processing_started_at",
            table: "feedback");
    }
}
