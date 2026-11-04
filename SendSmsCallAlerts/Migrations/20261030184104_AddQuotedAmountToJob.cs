
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SendSmsCallAlerts.Migrations
{
    public partial class AddQuotedAmountToJob : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // QuotedAmount is already added by:
            // 20261029162020_AddJobQuotedAmount.
            //
            // No additional schema changes are required.
            // Preserve all existing IvrOption records.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty.
            // QuotedAmount is owned by the earlier migration.
        }
    }
}
