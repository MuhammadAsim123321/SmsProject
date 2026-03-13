using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomHoursToAllScheduledJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomHours",
                table: "AllScheduledJobs",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomHours",
                table: "AllScheduledJobs");
        }
    }
}
