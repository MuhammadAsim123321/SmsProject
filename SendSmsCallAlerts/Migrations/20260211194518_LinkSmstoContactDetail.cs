using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class LinkSmstoContactDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContactDetailId",
                table: "SmsDetails",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsDetails_ContactDetailId",
                table: "SmsDetails",
                column: "ContactDetailId");

            migrationBuilder.AddForeignKey(
                name: "FK_SmsDetails_ContactDetail_ContactDetailId",
                table: "SmsDetails",
                column: "ContactDetailId",
                principalTable: "ContactDetail",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SmsDetails_ContactDetail_ContactDetailId",
                table: "SmsDetails");

            migrationBuilder.DropIndex(
                name: "IX_SmsDetails_ContactDetailId",
                table: "SmsDetails");

            migrationBuilder.DropColumn(
                name: "ContactDetailId",
                table: "SmsDetails");
        }
    }
}
