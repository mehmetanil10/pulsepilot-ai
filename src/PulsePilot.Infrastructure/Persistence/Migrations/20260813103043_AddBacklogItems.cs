using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulsePilot.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddBacklogItems : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "backlog_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_pending_action_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                priority = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_backlog_items", x => x.id);
                table.UniqueConstraint("ak_backlog_items_workspace_id_id", x => new { x.workspace_id, x.id });
                table.CheckConstraint("ck_backlog_items_priority", "priority IN ('P1', 'P2', 'P3', 'P4')");
                table.CheckConstraint("ck_backlog_items_status", "status IN ('Open', 'InProgress', 'Resolved', 'Closed')");
                table.ForeignKey(
                    name: "fk_backlog_items_actions_workspace_id_source_action_id",
                    columns: x => new { x.workspace_id, x.source_pending_action_id },
                    principalTable: "pending_actions",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_backlog_items_clusters_workspace_id_source_cluster_id",
                    columns: x => new { x.workspace_id, x.source_cluster_id },
                    principalTable: "feedback_clusters",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_backlog_items_members_workspace_id_created_by_user_id",
                    columns: x => new { x.workspace_id, x.created_by_user_id },
                    principalTable: "workspace_members",
                    principalColumns: new[] { "workspace_id", "user_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_backlog_items_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "ck_pending_actions_execution_state",
            table: "pending_actions",
            sql: "(status = 'Executed' AND executed_at IS NOT NULL) OR (status <> 'Executed' AND executed_at IS NULL)");

        migrationBuilder.CreateIndex(
            name: "ix_backlog_items_workspace_id_created_by_user_id",
            table: "backlog_items",
            columns: new[] { "workspace_id", "created_by_user_id" });

        migrationBuilder.CreateIndex(
            name: "ix_backlog_items_workspace_status_priority_created_at",
            table: "backlog_items",
            columns: new[] { "workspace_id", "status", "priority", "created_at" },
            descending: new[] { false, false, false, true });

        migrationBuilder.CreateIndex(
            name: "ux_backlog_items_active_source_cluster",
            table: "backlog_items",
            columns: new[] { "workspace_id", "source_cluster_id" },
            unique: true,
            filter: "status IN ('Open', 'InProgress')");

        migrationBuilder.CreateIndex(
            name: "ux_backlog_items_workspace_id_source_action_id",
            table: "backlog_items",
            columns: new[] { "workspace_id", "source_pending_action_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "backlog_items");

        migrationBuilder.DropCheckConstraint(
            name: "ck_pending_actions_execution_state",
            table: "pending_actions");
    }
}
