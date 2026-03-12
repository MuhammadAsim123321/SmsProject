using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddJobBookDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobBook",
                table: "JobSchedule");

            migrationBuilder.AddColumn<DateTime>(
                name: "JobBookDate",
                table: "JobSchedule",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JobBookDate",
                table: "JobSchedule");

            migrationBuilder.AddColumn<string>(
                name: "JobBook",
                table: "JobSchedule",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
