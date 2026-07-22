using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VibeCoreWeb.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledTaskHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PostgresQuartzSchema);

            migrationBuilder.CreateTable(
                name: "ScheduledTaskRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandlerKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    ErrorSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTaskRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTaskRuns_ScheduleId_StartedAt",
                table: "ScheduledTaskRuns",
                columns: new[] { "ScheduleId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTaskRuns_StartedAt",
                table: "ScheduledTaskRuns",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledTaskRuns");

            migrationBuilder.Sql(PostgresQuartzDropSchema);
        }
    }
}
