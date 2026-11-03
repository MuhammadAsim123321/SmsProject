using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class StaffLogoutType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<string>(
                name: "LogoutType",
                table: "UserLoginSessions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.Sql("UPDATE UserLoginSessions SET LogoutType = 'Manual' WHERE LogoutAt IS NOT NULL AND LogoutType IS NULL");
            
            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, Please confirm tomorrow's jobs:\n{jobsList}\nReply '{replyExample}' to confirm or call {officePhone}");

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 3,
                column: "TemplateBody",
                value: "Hi {customerName}, this is {companyName}. Your {category} appointment is confirmed for tomorrow, {jobDate}, at {jobTime} with {technicianName}. Questions? Call {officePhone}. Reply STOP to opt out.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoutType",
                table: "UserLoginSessions");

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "TemplateBody",
                value: "Hi {technicianFirstName}, please confirm tomorrow's jobs ({jobDate}):\n{jobsList}\n{jobConfirmationCode}\nCall {officePhone} with questions.");

            migrationBuilder.UpdateData(
                table: "AutomatedSmsSettings",
                keyColumn: "Id",
                keyValue: 3,
                column: "TemplateBody",
                value: "Hi {customerName}, reminder from {companyName}: your {category} appointment is tomorrow, {jobDate}, at {jobTime}. Technician: {technicianName}. Reply STOP to opt out.");
        }
    }
}
