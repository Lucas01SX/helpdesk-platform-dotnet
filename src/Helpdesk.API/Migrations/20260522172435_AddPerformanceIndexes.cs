using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_tickets_assignee_id",
                table: "tickets",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_customer_id",
                table: "tickets",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_sla_due_at_sla_breached_at",
                table: "tickets",
                columns: new[] { "sla_due_at", "sla_breached_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tickets_status_sla_breached_at",
                table: "tickets",
                columns: new[] { "status", "sla_breached_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_aggregate_id_occurred_at",
                table: "audit_events",
                columns: new[] { "aggregate_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_assignee_id",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_customer_id",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_sla_due_at_sla_breached_at",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_tickets_status_sla_breached_at",
                table: "tickets");

            migrationBuilder.DropIndex(
                name: "ix_audit_events_aggregate_id_occurred_at",
                table: "audit_events");
        }
    }
}
