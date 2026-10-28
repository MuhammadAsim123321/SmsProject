using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicianAndSplitAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Address",
                table: "ContactDetail",
                newName: "ZipCode");

            migrationBuilder.AddColumn<string>(
                name: "CityState",
                table: "SmsDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                table: "SmsDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "SmsDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedTechnicianId",
                table: "ContactDetail",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CityState",
                table: "ContactDetail",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StreetAddress",
                table: "ContactDetail",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Technicians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StreetAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CityState = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SelectedCategoryIds = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Technicians", x => x.Id);
                });

            

            migrationBuilder.CreateIndex(
                name: "IX_ContactDetail_AssignedTechnicianId",
                table: "ContactDetail",
                column: "AssignedTechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContactDetail_Technicians_AssignedTechnicianId",
                table: "ContactDetail",
                column: "AssignedTechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContactDetail_Technicians_AssignedTechnicianId",
                table: "ContactDetail");

            migrationBuilder.DropTable(
                name: "Technicians");

            migrationBuilder.DropIndex(
                name: "IX_ContactDetail_AssignedTechnicianId",
                table: "ContactDetail");

            

            migrationBuilder.DropColumn(
                name: "CityState",
                table: "SmsDetails");

            migrationBuilder.DropColumn(
                name: "StreetAddress",
                table: "SmsDetails");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "SmsDetails");

            migrationBuilder.DropColumn(
                name: "AssignedTechnicianId",
                table: "ContactDetail");

            migrationBuilder.DropColumn(
                name: "CityState",
                table: "ContactDetail");

            migrationBuilder.DropColumn(
                name: "StreetAddress",
                table: "ContactDetail");

            migrationBuilder.RenameColumn(
                name: "ZipCode",
                table: "ContactDetail",
                newName: "Address");
        }
    }
}
