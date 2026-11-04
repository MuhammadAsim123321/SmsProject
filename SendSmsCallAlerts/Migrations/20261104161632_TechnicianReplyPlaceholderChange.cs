using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class TechnicianReplyPlaceholderChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, please confirm tomorrow's jobs ({jobDate}):\r\n{jobsList}\r\n{jobConfirmationCode}\r\nCall {officePhone} with questions.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, Please confirm tomorrow's jobs\n({jobDate}):\n{jobsList}\nReply '{jobConfirmationCode}' to confirm or call {officePhone} with questions");
        }
    }
}
