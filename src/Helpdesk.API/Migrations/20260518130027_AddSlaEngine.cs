using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Helpdesk.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "auto_assigned_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "sla_breached_at",
                table: "tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sla_excluded",
                table: "tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "sla_score_applied",
                table: "tickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "sla_unassigned_penalty_count",
                table: "tickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "sla_monthly_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    tickets_within_sla = table.Column<int>(type: "integer", nullable: false),
                    tickets_breached = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sla_monthly_scores", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sla_monthly_scores_year_month",
                table: "sla_monthly_scores",
                columns: new[] { "year", "month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sla_monthly_scores");

            migrationBuilder.DropColumn(
                name: "auto_assigned_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_breached_at",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_excluded",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_score_applied",
                table: "tickets");

            migrationBuilder.DropColumn(
                name: "sla_unassigned_penalty_count",
                table: "tickets");
        }
    }
}
