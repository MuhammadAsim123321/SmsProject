using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddQuotedAmountToJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "IvrOption",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "IvrOption",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "IvrOption",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "IvrOption",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "IvrOption",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedAmount",
                table: "Jobs",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuotedAmount",
                table: "Jobs");

            migrationBuilder.InsertData(
                table: "IvrOption",
                columns: new[] { "Id", "Num", "TwilioNum", "UserId", "keyword" },
                values: new object[,]
                {
                    { 1, 1, "", 0, "HVAC Services" },
                    { 2, 2, "", 0, "Plumbing Repair" },
                    { 3, 3, "", 0, "Electrical Work" },
                    { 4, 4, "", 0, "Appliance Repair" },
                    { 5, 5, "", 0, "General Maintenance" }
                });
        }
    }
}
