using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulsePilot.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCustomerResponseDrafts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "customer_response_drafts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                feedback_cluster_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_pending_action_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_customer_response_drafts", x => x.id);
                table.UniqueConstraint("ak_customer_response_drafts_workspace_id_id", x => new { x.workspace_id, x.id });
                table.ForeignKey(
                    name: "fk_customer_response_drafts_actions_workspace_id_source_action_id",
                    columns: x => new { x.workspace_id, x.source_pending_action_id },
                    principalTable: "pending_actions",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_customer_response_drafts_clusters_workspace_id_cluster_id",
                    columns: x => new { x.workspace_id, x.feedback_cluster_id },
                    principalTable: "feedback_clusters",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_customer_response_drafts_feedback_workspace_id_feedback_id",
                    columns: x => new { x.workspace_id, x.feedback_id },
                    principalTable: "feedback",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_customer_response_drafts_members_workspace_id_created_by_user_id",
                    columns: x => new { x.workspace_id, x.created_by_user_id },
                    principalTable: "workspace_members",
                    principalColumns: new[] { "workspace_id", "user_id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_customer_response_drafts_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_customer_response_drafts_workspace_id_cluster_id",
            table: "customer_response_drafts",
            columns: new[] { "workspace_id", "feedback_cluster_id" });

        migrationBuilder.CreateIndex(
            name: "ix_customer_response_drafts_workspace_id_created_by_user_id",
            table: "customer_response_drafts",
            columns: new[] { "workspace_id", "created_by_user_id" });

        migrationBuilder.CreateIndex(
            name: "ix_customer_response_drafts_workspace_id_feedback_id",
            table: "customer_response_drafts",
            columns: new[] { "workspace_id", "feedback_id" });

        migrationBuilder.CreateIndex(
            name: "ux_customer_response_drafts_workspace_id_source_action_id",
            table: "customer_response_drafts",
            columns: new[] { "workspace_id", "source_pending_action_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "customer_response_drafts");
    }
}
