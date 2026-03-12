using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class smstble : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmsDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fromNum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    toNum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    smsBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    smsDirection = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsReadYesOrNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    dt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    createdOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    imgPath = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsDetails", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsDetails");
        }
    }
}
