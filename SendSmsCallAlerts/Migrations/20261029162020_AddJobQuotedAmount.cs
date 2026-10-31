using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    /// <inheritdoc />
    public partial class AddJobQuotedAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guard: migration 20261030184104_AddQuotedAmountToJob may have already added
            // this column on some local databases. Use IF NOT EXISTS to avoid a duplicate-column error.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[dbo].[Jobs]')
                      AND name = 'QuotedAmount'
                )
                BEGIN
                    ALTER TABLE [dbo].[Jobs] ADD [QuotedAmount] decimal(10,2) NULL;
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuotedAmount",
                table: "Jobs");
        }
    }
}
