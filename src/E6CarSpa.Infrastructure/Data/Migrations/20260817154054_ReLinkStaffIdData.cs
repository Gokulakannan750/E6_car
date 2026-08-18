using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    public partial class ReLinkStaffIdData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-link StaffAdvances to Staff using case-insensitive comparison.
            // The original AddStaffTable backfill used case-sensitive TRIM() matching,
            // so any case mismatch between WorkerName and FullName left StaffId as zero-GUID.
            migrationBuilder.Sql(@"
                UPDATE ""StaffAdvances"" sa
                SET ""StaffId"" = s.""Id""
                FROM ""Staff"" s
                WHERE sa.""StaffId"" = '00000000-0000-0000-0000-000000000000'
                  AND LOWER(TRIM(sa.""WorkerName"")) = LOWER(TRIM(s.""FullName""));
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No rollback — this is a one-way data repair.
        }
    }
}
