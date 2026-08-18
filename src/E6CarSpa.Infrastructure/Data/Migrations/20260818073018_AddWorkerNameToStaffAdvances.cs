using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
 public partial class AddWorkerNameToStaffAdvances : Migration
 {
 protected override void Up(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.AddColumn<string>(
 name: "WorkerName",
 table: "StaffAdvances",
 type: "varchar(120)",
 maxLength: 120,
 nullable: false,
 defaultValue: "");

 migrationBuilder.CreateIndex(
 name: "IX_StaffAdvances_WorkerName",
 table: "StaffAdvances",
 column: "WorkerName");

 // Backfill WorkerName from the Staff table
 migrationBuilder.Sql(@"
 UPDATE ""StaffAdvances"" sa
 SET ""WorkerName"" = s.""FullName""
 FROM ""Staff"" s
 WHERE sa.""StaffId"" = s.""Id"" AND sa.""WorkerName"" = '';
 ");

 // Add FK constraint if not already present
 migrationBuilder.Sql(@"
 DO $$
 BEGIN
 IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_StaffAdvances_Staff_StaffId') THEN
 ALTER TABLE ""StaffAdvances""
 ADD CONSTRAINT ""FK_StaffAdvances_Staff_StaffId""
 FOREIGN KEY (""StaffId"") REFERENCES ""Staff"" (""Id"") ON DELETE RESTRICT;
 END IF;
 END $$;
 ");
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.DropIndex(
 name: "IX_StaffAdvances_WorkerName",
 table: "StaffAdvances");

 migrationBuilder.DropColumn(
 name: "WorkerName",
 table: "StaffAdvances");
 }
 }
}
