using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddContactCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "ContactDetail",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "ContactDetail");
        }
    }
}
