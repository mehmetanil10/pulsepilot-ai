using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulsePilot.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFeedbackAnalysis : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddUniqueConstraint(
            name: "ak_feedback_workspace_id_id",
            table: "feedback",
            columns: new[] { "workspace_id", "id" });

        migrationBuilder.CreateTable(
            name: "feedback_analyses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                component = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                severity = table.Column<int>(type: "integer", nullable: false),
                sentiment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                suggested_action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_feedback_analyses", x => x.id);
                table.CheckConstraint("ck_feedback_analyses_category", "category IN ('Bug', 'FeatureRequest', 'Complaint', 'Question', 'Praise', 'Other')");
                table.CheckConstraint("ck_feedback_analyses_component", "component IN ('Payments', 'Authentication', 'Dashboard', 'Reporting', 'Mobile', 'Api', 'Performance', 'General')");
                table.CheckConstraint("ck_feedback_analyses_confidence", "confidence BETWEEN 0 AND 1");
                table.CheckConstraint("ck_feedback_analyses_sentiment", "sentiment IN ('Positive', 'Neutral', 'Negative')");
                table.CheckConstraint("ck_feedback_analyses_severity", "severity BETWEEN 1 AND 5");
                table.ForeignKey(
                    name: "fk_feedback_analyses_feedback_workspace_id_feedback_id",
                    columns: x => new { x.workspace_id, x.feedback_id },
                    principalTable: "feedback",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_feedback_analyses_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_feedback_analyses_workspace_id_feedback_id",
            table: "feedback_analyses",
            columns: new[] { "workspace_id", "feedback_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "feedback_analyses");

        migrationBuilder.DropUniqueConstraint(
            name: "ak_feedback_workspace_id_id",
            table: "feedback");
    }
}
