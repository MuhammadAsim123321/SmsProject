using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class optOutAndSchedulerFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RunFromId",
                table: "Schedulers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeToRunId",
                table: "Schedulers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomDate",
                table: "AllScheduledJobs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HangfireJobLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HangfireJobLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OptOuts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptOuts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RunFrom",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RunFromName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RunFrom", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeToRun",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HourCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeToRun", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RunFrom",
                columns: new[] { "Id", "RunFromName" },
                values: new object[,]
                {
                    { 1, "Today Date" },
                    { 2, "Custom Date" },
                    { 3, "Job Book Date" },
                    { 4, "Job Created Date" }
                });

            migrationBuilder.InsertData(
                table: "TimeToRun",
                columns: new[] { "Id", "HourCount", "Name" },
                values: new object[,]
                {
                    { 1, 0, "Now" },
                    { 2, 24, "24 Hour" },
                    { 3, 48, "48 Hour" },
                    { 4, 72, "3 days" },
                    { 5, 96, "4 days" },
                    { 6, 120, "5 days" },
                    { 7, 144, "6 days" },
                    { 8, 168, "7 days" },
                    { 9, 720, "1 Month" },
                    { 10, 4320, "6 Months" },
                    { 11, 8760, "1 Year" },
                    { 12, -1, "Enter Custom Days and Hours" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Schedulers_RunFromId",
                table: "Schedulers",
                column: "RunFromId");

            migrationBuilder.CreateIndex(
                name: "IX_Schedulers_TimeToRunId",
                table: "Schedulers",
                column: "TimeToRunId");

            migrationBuilder.AddForeignKey(
                name: "FK_Schedulers_RunFrom_RunFromId",
                table: "Schedulers",
                column: "RunFromId",
                principalTable: "RunFrom",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedulers_TimeToRun_TimeToRunId",
                table: "Schedulers",
                column: "TimeToRunId",
                principalTable: "TimeToRun",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schedulers_RunFrom_RunFromId",
                table: "Schedulers");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedulers_TimeToRun_TimeToRunId",
                table: "Schedulers");

            migrationBuilder.DropTable(
                name: "HangfireJobLogs");

            migrationBuilder.DropTable(
                name: "OptOuts");

            migrationBuilder.DropTable(
                name: "RunFrom");

            migrationBuilder.DropTable(
                name: "TimeToRun");

            migrationBuilder.DropIndex(
                name: "IX_Schedulers_RunFromId",
                table: "Schedulers");

            migrationBuilder.DropIndex(
                name: "IX_Schedulers_TimeToRunId",
                table: "Schedulers");

            migrationBuilder.DropColumn(
                name: "RunFromId",
                table: "Schedulers");

            migrationBuilder.DropColumn(
                name: "TimeToRunId",
                table: "Schedulers");

            migrationBuilder.DropColumn(
                name: "CustomDate",
                table: "AllScheduledJobs");
        }
    }
}
