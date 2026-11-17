using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameNormalizedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsernameNormalized",
                table: "User",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [User]
                SET UsernameNormalized = UPPER(LTRIM(RTRIM(Username)))
                WHERE LTRIM(RTRIM(ISNULL(Username,''))) <> '';
                """);

            migrationBuilder.CreateIndex(
                name: "UQ_User_UsernameNormalized",
                table: "User",
                column: "UsernameNormalized",
                unique: true,
                filter: "[UsernameNormalized] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_User_UsernameNormalized",
                table: "User");

            migrationBuilder.DropColumn(
                name: "UsernameNormalized",
                table: "User");
        }
    }
}
