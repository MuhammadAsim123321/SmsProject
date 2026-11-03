using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class TechnicianJobListsAndTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, please confirm tomorrow's jobs ({jobDate}):\n{jobsList}\n{jobConfirmationCode}\nCall {officePhone} with questions.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, please confirm tomorrow's jobs ({jobDate}):\n{jobsList}\nReply 'C 123' to confirm or call {officePhone}.");
        }
    }
}
