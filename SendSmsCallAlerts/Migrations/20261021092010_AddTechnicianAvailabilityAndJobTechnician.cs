using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianAvailabilityAndJobTechnician : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DistanceMiles",
                table: "AllScheduledJobs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TechnicianId",
                table: "AllScheduledJobs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TechnicianAvailabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TechnicianId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianAvailabilities_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllScheduledJobs_TechnicianId",
                table: "AllScheduledJobs",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "UQ_TechnicianAvailability_Tech_Day",
                table: "TechnicianAvailabilities",
                columns: new[] { "TechnicianId", "DayOfWeek" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AllScheduledJobs_Technicians_TechnicianId",
                table: "AllScheduledJobs",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AllScheduledJobs_Technicians_TechnicianId",
                table: "AllScheduledJobs");

            migrationBuilder.DropTable(
                name: "TechnicianAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_AllScheduledJobs_TechnicianId",
                table: "AllScheduledJobs");

            migrationBuilder.DropColumn(
                name: "DistanceMiles",
                table: "AllScheduledJobs");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "AllScheduledJobs");
        }
    }
}
