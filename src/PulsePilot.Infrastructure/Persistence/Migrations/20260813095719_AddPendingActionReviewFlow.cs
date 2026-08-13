using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PulsePilot.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPendingActionReviewFlow : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "ck_pending_actions_review_state",
            table: "pending_actions",
            sql: "(status = 'Pending' AND approved_at IS NULL AND rejected_at IS NULL) OR (status = 'Approved' AND approved_at IS NOT NULL AND rejected_at IS NULL) OR (status = 'Rejected' AND approved_at IS NULL AND rejected_at IS NOT NULL) OR (status IN ('Executed', 'Failed') AND approved_at IS NOT NULL AND rejected_at IS NULL)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_pending_actions_review_state",
            table: "pending_actions");
    }
}
