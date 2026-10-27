using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddSmsLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmsLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MessageContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    JobId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SettingName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NotificationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RecipientType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RecipientId = table.Column<int>(type: "int", nullable: true),
                    RecipientName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsSimulated = table.Column<bool>(type: "bit", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SmsDetailId = table.Column<int>(type: "int", nullable: true),
                    AutomatedSmsLogId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmsLogs_AutomatedSmsLogs_AutomatedSmsLogId",
                        column: x => x.AutomatedSmsLogId,
                        principalTable: "AutomatedSmsLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SmsLogs_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_AutomatedSmsLogId",
                table: "SmsLogs",
                column: "AutomatedSmsLogId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_CreatedOn",
                table: "SmsLogs",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_JobId",
                table: "SmsLogs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_RecipientNumber",
                table: "SmsLogs",
                column: "RecipientNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_SmsDetailId",
                table: "SmsLogs",
                column: "SmsDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_Status_NextAttempt",
                table: "SmsLogs",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmsLogs");
        }
    }
}
