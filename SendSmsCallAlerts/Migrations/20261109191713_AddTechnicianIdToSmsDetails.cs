using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianIdToSmsDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TechnicianId",
                table: "SmsDetails",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmsDetails_TechnicianId",
                table: "SmsDetails",
                column: "TechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_SmsDetails_Technicians_TechnicianId",
                table: "SmsDetails",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SmsDetails_Technicians_TechnicianId",
                table: "SmsDetails");

            migrationBuilder.DropIndex(
                name: "IX_SmsDetails_TechnicianId",
                table: "SmsDetails");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "SmsDetails");
        }
    }
}
