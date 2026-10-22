using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAutomatedSmsTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                column: "TemplateBody",
                value: "Hi {customerName}, this is a reminder from {companyName}: your {category} appointment is scheduled for tomorrow, {jobDate}, at {jobTime}. Your technician will be {technicianName}. Reply STOP to opt out.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, please reply with your availability for the week of {weekStart} to {weekEnd}. Example: MON 9AM-5PM, TUE OFF. Thank you, {companyName}.");

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, you have {jobCount} job(s) tomorrow ({jobDate}): {jobsList} - {companyName}");

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 3,
                column: "TemplateBody",
                value: "Hi {customerName}, reminder: your {category} appointment is tomorrow {jobDate} at {jobTime} with {technicianName}. - {companyName}");
        }
    }
}
