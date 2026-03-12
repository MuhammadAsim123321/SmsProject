using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AllScheduledjob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllScheduledJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactDetailId = table.Column<int>(type: "int", nullable: false),
                    SchedulerId = table.Column<int>(type: "int", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    JobBookDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    JobCompletedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllScheduledJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllScheduledJobs_ContactDetail_ContactDetailId",
                        column: x => x.ContactDetailId,
                        principalTable: "ContactDetail",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AllScheduledJobs_Schedulers_SchedulerId",
                        column: x => x.SchedulerId,
                        principalTable: "Schedulers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllScheduledJobs_ContactDetailId",
                table: "AllScheduledJobs",
                column: "ContactDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AllScheduledJobs_SchedulerId",
                table: "AllScheduledJobs",
                column: "SchedulerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllScheduledJobs");
        }
    }
}
