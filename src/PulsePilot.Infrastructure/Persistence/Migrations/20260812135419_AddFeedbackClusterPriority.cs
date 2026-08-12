using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulsePilot.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFeedbackClusterPriority : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "priority",
            table: "feedback_clusters",
            type: "character varying(2)",
            maxLength: 2,
            nullable: false,
            defaultValue: "P4");

        migrationBuilder.AddColumn<decimal>(
            name: "priority_score",
            table: "feedback_clusters",
            type: "numeric(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.CreateIndex(
            name: "ix_feedback_clusters_workspace_id_priority_score",
            table: "feedback_clusters",
            columns: ["workspace_id", "priority_score"],
            descending: [false, true]);

        migrationBuilder.AddCheckConstraint(
            name: "ck_feedback_clusters_priority",
            table: "feedback_clusters",
            sql: "priority IN ('P1', 'P2', 'P3', 'P4')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_feedback_clusters_priority_score",
            table: "feedback_clusters",
            sql: "priority_score BETWEEN 0 AND 100");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_feedback_clusters_workspace_id_priority_score",
            table: "feedback_clusters");

        migrationBuilder.DropCheckConstraint(
            name: "ck_feedback_clusters_priority",
            table: "feedback_clusters");

        migrationBuilder.DropCheckConstraint(
            name: "ck_feedback_clusters_priority_score",
            table: "feedback_clusters");

        migrationBuilder.DropColumn(
            name: "priority",
            table: "feedback_clusters");

        migrationBuilder.DropColumn(
            name: "priority_score",
            table: "feedback_clusters");
    }
}
