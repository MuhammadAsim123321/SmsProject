using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddReplyToSmsId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReplyToSmsId",
                table: "SmsDetails",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsDetails_ReplyToSmsId",
                table: "SmsDetails",
                column: "ReplyToSmsId");

            migrationBuilder.AddForeignKey(
                name: "FK_SmsDetails_SmsDetails_ReplyToSmsId",
                table: "SmsDetails",
                column: "ReplyToSmsId",
                principalTable: "SmsDetails",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SmsDetails_SmsDetails_ReplyToSmsId",
                table: "SmsDetails");

            migrationBuilder.DropIndex(
                name: "IX_SmsDetails_ReplyToSmsId",
                table: "SmsDetails");

            migrationBuilder.DropColumn(
                name: "ReplyToSmsId",
                table: "SmsDetails");
        }
    }
}
