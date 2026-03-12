using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class individualusers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TwilioNum",
                table: "OptionInstruction",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "OptionInstruction",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TwilioNum",
                table: "IvrOption",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "IvrOption",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TwNum",
                table: "IntroSms",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwilioNum",
                table: "OptionInstruction");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "OptionInstruction");

            migrationBuilder.DropColumn(
                name: "TwilioNum",
                table: "IvrOption");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "IvrOption");

            migrationBuilder.DropColumn(
                name: "TwNum",
                table: "IntroSms");
        }
    }
}
