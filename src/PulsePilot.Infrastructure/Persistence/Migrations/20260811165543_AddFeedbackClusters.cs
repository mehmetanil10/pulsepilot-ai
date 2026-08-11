using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulsePilot.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFeedbackClusters : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "feedback_cluster_id",
            table: "feedback",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "feedback_clusters",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                component = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_feedback_clusters", x => x.id);
                table.UniqueConstraint("ak_feedback_clusters_workspace_id_id", x => new { x.workspace_id, x.id });
                table.CheckConstraint("ck_feedback_clusters_category", "category IN ('Bug', 'FeatureRequest', 'Complaint', 'Question', 'Praise', 'Other')");
                table.CheckConstraint("ck_feedback_clusters_component", "component IN ('Payments', 'Authentication', 'Dashboard', 'Reporting', 'Mobile', 'Api', 'Performance', 'General')");
                table.ForeignKey(
                    name: "fk_feedback_clusters_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_feedback_workspace_id_cluster_id",
            table: "feedback",
            columns: new[] { "workspace_id", "feedback_cluster_id" });

        migrationBuilder.CreateIndex(
            name: "ix_feedback_clusters_workspace_category_component",
            table: "feedback_clusters",
            columns: new[] { "workspace_id", "category", "component" });

        migrationBuilder.CreateIndex(
            name: "ix_feedback_clusters_workspace_id_updated_at",
            table: "feedback_clusters",
            columns: new[] { "workspace_id", "updated_at" },
            descending: new[] { false, true });

        migrationBuilder.AddForeignKey(
            name: "fk_feedback_clusters_workspace_id_cluster_id",
            table: "feedback",
            columns: new[] { "workspace_id", "feedback_cluster_id" },
            principalTable: "feedback_clusters",
            principalColumns: new[] { "workspace_id", "id" },
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_feedback_clusters_workspace_id_cluster_id",
            table: "feedback");

        migrationBuilder.DropTable(
            name: "feedback_clusters");

        migrationBuilder.DropIndex(
            name: "ix_feedback_workspace_id_cluster_id",
            table: "feedback");

        migrationBuilder.DropColumn(
            name: "feedback_cluster_id",
            table: "feedback");
    }
}
