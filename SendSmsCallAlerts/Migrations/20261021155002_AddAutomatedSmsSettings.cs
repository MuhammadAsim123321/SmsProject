using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomatedSmsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomatedSmsSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NotificationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecipientType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    SendTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    TemplateBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomatedSmsSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomatedSmsLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingId = table.Column<int>(type: "int", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecipientType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecipientId = table.Column<int>(type: "int", nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SmsDetailId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomatedSmsLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomatedSmsLogs_AutomatedSmsSettings_SettingId",
                        column: x => x.SettingId,
                        principalTable: "AutomatedSmsSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AutomatedSmsSettings",
                columns: new[] { "Id", "CreatedOn", "DayOfWeek", "Frequency", "IsActive", "LastRunAt", "Name", "NotificationType", "RecipientType", "SendTime", "TemplateBody", "UpdatedOn" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), 0, "Weekly", true, null, "Technician Weekly Availability Request", "TechnicianAvailabilityRequest", "Technician", new TimeSpan(0, 17, 0, 0, 0), "Hi {technicianFirstName}, please reply with your availability for the week of {weekStart} to {weekEnd}. Example: MON 9AM-5PM, TUE OFF. Thank you, {companyName}.", null },
                    { 2, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), null, "Daily", true, null, "Technician Daily Job Summary", "TechnicianDailyJobSummary", "Technician", new TimeSpan(0, 17, 0, 0, 0), "Hi {technicianFirstName}, you have {jobCount} job(s) tomorrow ({jobDate}): {jobsList} - {companyName}", null },
                    { 3, new DateTime(2026, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), null, "Daily", true, null, "Customer Next-Day Job Reminder", "CustomerJobReminder", "Customer", new TimeSpan(0, 17, 0, 0, 0), "Hi {customerName}, reminder: your {category} appointment is tomorrow {jobDate} at {jobTime} with {technicianName}. - {companyName}", null }
                });

            migrationBuilder.CreateIndex(
                name: "UQ_AutomatedSmsLogs_Occurrence",
                table: "AutomatedSmsLogs",
                columns: new[] { "SettingId", "OccurrenceKey", "RecipientType", "RecipientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_AutomatedSmsSettings_Name",
                table: "AutomatedSmsSettings",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomatedSmsLogs");

            migrationBuilder.DropTable(
                name: "AutomatedSmsSettings");
        }
    }
}
