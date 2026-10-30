using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianJobLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechnicianJobLists",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TechnicianId = table.Column<int>(type: "int", nullable: false),
                    JobDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    JobId = table.Column<int>(type: "int", nullable: false),
                    JobStartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianJobLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianJobLists_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicianJobLists_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, please reply with your availability for the week of {weekStart} to {weekEnd}.\nExample: MON 9AM-5PM, TUE OFF\nThank you, {companyName}");

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, please confirm tomorrow's jobs ({jobDate}):\n{jobsList}\nReply 'C 123' to confirm or call {officePhone}.");

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "SendTime", "TemplateBody" },
                values: new object[] { new TimeSpan(0, 19, 0, 0, 0), "Hi {customerName}, reminder from {companyName}: your {category} appointment is tomorrow, {jobDate}, at {jobTime}. Technician: {technicianName}. Reply STOP to opt out." });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianJobLists_JobDate",
                table: "TechnicianJobLists",
                column: "JobDate");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianJobLists_JobId",
                table: "TechnicianJobLists",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "UQ_TechnicianJobLists_Tech_Position",
                table: "TechnicianJobLists",
                columns: new[] { "TechnicianId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnicianJobLists");

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, please reply with your availability for the week of {weekStart} to {weekEnd}.\n\nExample: MON 9AM-5PM, TUE OFF\n\nThank you, {companyName}");

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, you have {jobCount} job(s) tomorrow ({jobDate}):\n\n{jobsList}\n\n- {companyName}");

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "SendTime", "TemplateBody" },
                values: new object[] { new TimeSpan(0, 17, 0, 0, 0), "Hi {customerName}, this is a reminder from {companyName}: your {category} appointment is scheduled for tomorrow, {jobDate}, at {jobTime}. Your technician will be {technicianName}. Reply STOP to opt out." });
        }
    }
}
