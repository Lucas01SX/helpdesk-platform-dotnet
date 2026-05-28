using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.API.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoAssignedAtStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_tickets_auto_assigned_at_status",
                table: "tickets",
                columns: new[] { "auto_assigned_at", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tickets_auto_assigned_at_status",
                table: "tickets");
        }
    }
}
