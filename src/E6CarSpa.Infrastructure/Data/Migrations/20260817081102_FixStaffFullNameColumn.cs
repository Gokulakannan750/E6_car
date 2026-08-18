using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations;

public partial class FixStaffFullNameColumn : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            ALTER TABLE ""Staff"" ADD COLUMN IF NOT EXISTS ""FullName"" varchar(120) NOT NULL DEFAULT '';
            ALTER TABLE ""Staff"" ALTER COLUMN ""FullName"" DROP DEFAULT;
            CREATE INDEX IF NOT EXISTS ""IX_Staff_FullName"" ON ""Staff"" (""FullName"");
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Staff_FullName", table: "Staff");
        migrationBuilder.DropColumn(name: "FullName", table: "Staff");
    }
}
