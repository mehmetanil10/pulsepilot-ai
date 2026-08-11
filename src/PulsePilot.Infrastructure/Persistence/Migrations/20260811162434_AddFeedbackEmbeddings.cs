using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace PulsePilot.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddFeedbackEmbeddings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:vector", ",,");

        migrationBuilder.CreateTable(
            name: "feedback_embeddings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                feedback_id = table.Column<Guid>(type: "uuid", nullable: false),
                model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                source_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                embedding = table.Column<Vector>(type: "vector(1536)", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_feedback_embeddings", x => x.id);
                table.ForeignKey(
                    name: "fk_feedback_embeddings_feedback_workspace_id_feedback_id",
                    columns: x => new { x.workspace_id, x.feedback_id },
                    principalTable: "feedback",
                    principalColumns: new[] { "workspace_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_feedback_embeddings_workspaces_workspace_id",
                    column: x => x.workspace_id,
                    principalTable: "workspaces",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_feedback_embeddings_embedding_cosine",
            table: "feedback_embeddings",
            column: "embedding")
            .Annotation("Npgsql:IndexMethod", "hnsw")
            .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
            .Annotation("Npgsql:StorageParameter:ef_construction", 64)
            .Annotation("Npgsql:StorageParameter:m", 16);

        migrationBuilder.CreateIndex(
            name: "ux_feedback_embeddings_workspace_id_feedback_id",
            table: "feedback_embeddings",
            columns: new[] { "workspace_id", "feedback_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "feedback_embeddings");

        migrationBuilder.AlterDatabase()
            .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
    }
}
