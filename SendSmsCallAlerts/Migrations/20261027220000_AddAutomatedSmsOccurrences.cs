using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    public partial class AddAutomatedSmsOccurrences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomatedSmsOccurrences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettingId = table.Column<int>(type: "int", nullable: false),
                    ScheduledForLocal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalRecipients = table.Column<int>(type: "int", nullable: false),
                    QueuedCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomatedSmsOccurrences", x => x.Id);
                    table.ForeignKey("FK_AutomatedSmsOccurrences_AutomatedSmsSettings_SettingId",
                        x => x.SettingId, "AutomatedSmsSettings", "Id", onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex("UQ_AutomatedSmsOccurrences_Setting_Local",
                "AutomatedSmsOccurrences", new[] { "SettingId", "ScheduledForLocal" }, unique: true);
            migrationBuilder.CreateIndex("IX_AutomatedSmsOccurrences_Status_Utc",
                "AutomatedSmsOccurrences", new[] { "Status", "ScheduledForUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable(name: "AutomatedSmsOccurrences");
    }
}
