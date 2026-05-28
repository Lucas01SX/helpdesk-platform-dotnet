using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrelationIdToAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "correlation_id",
                table: "audit_events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "audit_events");
        }
    }
}
