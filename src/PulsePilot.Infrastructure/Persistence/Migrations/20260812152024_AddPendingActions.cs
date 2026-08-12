using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulsePilot.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPendingActions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "pending_actions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                feedback_cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                action_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                payload = table.Column<string>(type: "jsonb", maxLength: 8000, nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                rejected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_pending_actions", x => x.id);
                table.UniqueConstraint("ak_pending_actions_workspace_id_id", x => new { x.workspace_id, x.id });
                table.CheckConstraint("ck_pending_actions_action_type", "action_type IN ('CreateEngineeringIssue', 'DraftCustomerResponse', 'GenerateReport', 'EscalateIssue')");
                table.CheckConstraint("ck_pending_actions_payload_object", "jsonb_typeof(payload) = 'object'");
                table.CheckConstraint("ck_pending_actions_status", "status IN ('Pending', 'Approved', 'Rejected', 'Executed', 'Failed')");
                table.ForeignKey(
                    name: "fk_pending_actions_clusters_workspace_id_cluster_id",
                    columns: x => new { x.workspace_id, x.feedback_cluster_id },
                    principalTable: "feedback_clusters",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_pending_actions_feedback_workspace_id_feedback_id",
                    columns: x => new { x.workspace_id, x.feedback_id },
                    principalTable: "feedback",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_pending_actions_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_pending_actions_workspace_id_feedback_id",
            table: "pending_actions",
            columns: new[] { "workspace_id", "feedback_id" });

        migrationBuilder.CreateIndex(
            name: "ix_pending_actions_workspace_status_created_at",
            table: "pending_actions",
            columns: new[] { "workspace_id", "status", "created_at" },
            descending: new[] { false, false, true });

        migrationBuilder.CreateIndex(
            name: "ux_pending_actions_active_cluster_action_type",
            table: "pending_actions",
            columns: new[] { "workspace_id", "feedback_cluster_id", "action_type" },
            unique: true,
            filter: "status IN ('Pending', 'Approved')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "pending_actions");
    }
}
