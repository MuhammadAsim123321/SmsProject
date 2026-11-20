using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class EmailNormalizedAndUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailNormalized",
                table: "User",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [User]
                SET EmailNormalized = LOWER(LTRIM(RTRIM(Email)))
                WHERE LTRIM(RTRIM(ISNULL(Email,''))) <> '';

                UPDATE [User]
                SET EmailNormalized = NULL
                WHERE EmailNormalized = '';
                """);

            migrationBuilder.CreateIndex(
                name: "UX_User_EmailNormalized",
                table: "User",
                column: "EmailNormalized",
                unique: true,
                filter: "[EmailNormalized] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_User_EmailNormalized",
                table: "User");

            migrationBuilder.DropColumn(
                name: "EmailNormalized",
                table: "User");

        }
    }
}
