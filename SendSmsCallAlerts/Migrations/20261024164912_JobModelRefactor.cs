using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class JobModelRefactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AllScheduledJobs_ContactDetail_ContactDetailId",
                table: "AllScheduledJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_AllScheduledJobs_Technicians_TechnicianId",
                table: "AllScheduledJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_ContactDetail_Technicians_AssignedTechnicianId",
                table: "ContactDetail");

            migrationBuilder.DropTable(
                name: "JobSchedule");

            migrationBuilder.DropIndex(
                name: "IX_ContactDetail_AssignedTechnicianId",
                table: "ContactDetail");

            migrationBuilder.DropIndex(
                name: "IX_AllScheduledJobs_ContactDetailId",
                table: "AllScheduledJobs");

            migrationBuilder.DropIndex(
                name: "IX_AllScheduledJobs_TechnicianId",
                table: "AllScheduledJobs");

            migrationBuilder.AlterColumn<string>(
                name: "toNum",
                table: "Schedulers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "fromNum",
                table: "Schedulers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "ContactDetail",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactDetailId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    TechnicianId = table.Column<int>(type: "int", nullable: true),
                    StartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DistanceMiles = table.Column<double>(type: "float", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceSmsId = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_ContactDetail_ContactDetailId",
                        column: x => x.ContactDetailId,
                        principalTable: "ContactDetail",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Jobs_IvrOption_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "IvrOption",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Jobs_SmsDetails_SourceSmsId",
                        column: x => x.SourceSmsId,
                        principalTable: "SmsDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Jobs_Technicians_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Technicians",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                BEGIN TRY
                    SET IDENTITY_INSERT dbo.Jobs ON;

                    INSERT INTO dbo.Jobs
                        (Id, ContactDetailId, CategoryId, TechnicianId, StartAt, EndAt,
                         DistanceMiles, Status, SourceSmsId, CreatedOn, UpdatedOn)
                    SELECT
                        j.Id,
                        j.ContactDetailId,
                        CASE
                            WHEN EXISTS (SELECT 1 FROM dbo.IvrOption i WHERE i.Id = c.CategoryId)
                                THEN c.CategoryId
                            WHEN EXISTS (SELECT 1 FROM dbo.IvrOption i WHERE i.Id = s.templateOrAudioId)
                                THEN s.templateOrAudioId
                            ELSE NULL
                        END,
                        CASE
                            WHEN EXISTS (SELECT 1 FROM dbo.Technicians t WHERE t.Id = j.TechnicianId)
                                THEN j.TechnicianId
                            WHEN EXISTS (SELECT 1 FROM dbo.Technicians t WHERE t.Id = c.AssignedTechnicianId)
                                THEN c.AssignedTechnicianId
                            ELSE NULL
                        END,
                        COALESCE(j.JobBookDate, j.AssignedDate),
                        COALESCE(j.JobCompletedDate, j.JobBookDate, j.AssignedDate),
                        j.DistanceMiles,
                        N'Scheduled',
                        NULL,
                        j.AssignedDate,
                        NULL
                    FROM dbo.AllScheduledJobs j
                    INNER JOIN dbo.ContactDetail c ON c.Id = j.ContactDetailId
                    LEFT JOIN dbo.Schedulers s ON s.Id = j.SchedulerId;

                    SET IDENTITY_INSERT dbo.Jobs OFF;

                    UPDATE dbo.AllScheduledJobs
                    SET ContactDetailId = Id;
                END TRY
                BEGIN CATCH
                    SET IDENTITY_INSERT dbo.Jobs OFF;
                    THROW;
                END CATCH;
                """);

            migrationBuilder.RenameColumn(
                name: "ContactDetailId",
                table: "AllScheduledJobs",
                newName: "JobId");

            migrationBuilder.AlterColumn<int>(
                name: "JobId",
                table: "AllScheduledJobs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

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
                name: "CategoryId",
                table: "ContactDetail");

            migrationBuilder.DropColumn(
                name: "DistanceMiles",
                table: "AllScheduledJobs");

            migrationBuilder.DropColumn(
                name: "JobBookDate",
                table: "AllScheduledJobs");

            migrationBuilder.DropColumn(
                name: "JobCompletedDate",
                table: "AllScheduledJobs");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "AllScheduledJobs");

            migrationBuilder.UpdateData(
                table: "RunFrom",
                keyColumn: "Id",
                keyValue: 4,
                column: "RunFromName",
                value: "Job End Date");

            migrationBuilder.CreateIndex(
                name: "IX_SmsDetails_ScheduledJobId",
                table: "SmsDetails",
                column: "ScheduledJobId");

            migrationBuilder.CreateIndex(
                name: "UQ_ContactDetail_Phone",
                table: "ContactDetail",
                column: "PhoneNumber",
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_AllScheduledJobs_Job_Scheduler",
                table: "AllScheduledJobs",
                columns: new[] { "JobId", "SchedulerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CategoryId",
                table: "Jobs",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ContactDetailId",
                table: "Jobs",
                column: "ContactDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_SourceSmsId",
                table: "Jobs",
                column: "SourceSmsId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Technician_Time",
                table: "Jobs",
                columns: new[] { "TechnicianId", "StartAt", "EndAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AllScheduledJobs_Jobs_JobId",
                table: "AllScheduledJobs",
                column: "JobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Remove invalid job references, not SMS records.
            migrationBuilder.Sql(
                """
                UPDATE s
                SET s.ScheduledJobId = NULL
                FROM dbo.SmsDetails AS s
                LEFT JOIN dbo.AllScheduledJobs AS j
                    ON j.Id = s.ScheduledJobId
                WHERE s.ScheduledJobId IS NOT NULL
                  AND j.Id IS NULL;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_SmsDetails_AllScheduledJobs_ScheduledJobId",
                table: "SmsDetails",
                column: "ScheduledJobId",
                principalTable: "AllScheduledJobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AllScheduledJobs_Jobs_JobId",
                table: "AllScheduledJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_SmsDetails_AllScheduledJobs_ScheduledJobId",
                table: "SmsDetails");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_SmsDetails_ScheduledJobId",
                table: "SmsDetails");

            migrationBuilder.DropIndex(
                name: "UQ_ContactDetail_Phone",
                table: "ContactDetail");

            migrationBuilder.DropIndex(
                name: "UQ_AllScheduledJobs_Job_Scheduler",
                table: "AllScheduledJobs");

            migrationBuilder.AlterColumn<int>(
                name: "JobId",
                table: "AllScheduledJobs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.RenameColumn(
                name: "JobId",
                table: "AllScheduledJobs",
                newName: "ContactDetailId");

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

            migrationBuilder.AlterColumn<string>(
                name: "toNum",
                table: "Schedulers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "fromNum",
                table: "Schedulers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "ContactDetail",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedTechnicianId",
                table: "ContactDetail",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "ContactDetail",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DistanceMiles",
                table: "AllScheduledJobs",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "JobBookDate",
                table: "AllScheduledJobs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "JobCompletedDate",
                table: "AllScheduledJobs",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "TechnicianId",
                table: "AllScheduledJobs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobSchedule",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactDetailId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPaused = table.Column<bool>(type: "bit", nullable: false),
                    JobBookDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Scheduler = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSchedule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobSchedule_ContactDetail_ContactDetailId",
                        column: x => x.ContactDetailId,
                        principalTable: "ContactDetail",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "RunFrom",
                keyColumn: "Id",
                keyValue: 4,
                column: "RunFromName",
                value: "Job Created Date");

            migrationBuilder.CreateIndex(
                name: "IX_ContactDetail_AssignedTechnicianId",
                table: "ContactDetail",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_AllScheduledJobs_ContactDetailId",
                table: "AllScheduledJobs",
                column: "ContactDetailId");

            migrationBuilder.CreateIndex(
                name: "IX_AllScheduledJobs_TechnicianId",
                table: "AllScheduledJobs",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_JobSchedule_ContactDetailId",
                table: "JobSchedule",
                column: "ContactDetailId");



            migrationBuilder.AddForeignKey(
                name: "FK_AllScheduledJobs_ContactDetail_ContactDetailId",
                table: "AllScheduledJobs",
                column: "ContactDetailId",
                principalTable: "ContactDetail",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AllScheduledJobs_Technicians_TechnicianId",
                table: "AllScheduledJobs",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ContactDetail_Technicians_AssignedTechnicianId",
                table: "ContactDetail",
                column: "AssignedTechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");
        }
    }
}
