using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
 public partial class FixShowroomDailyStaffTimestampColumns : Migration
 {
 protected override void Up(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.AlterColumn<DateTime>(
 name: "AssignmentDate",
 table: "ShowroomDailyStaff",
 type: "timestamp without time zone",
 nullable: false,
 oldClrType: typeof(DateTime),
 oldType: "timestamp with time zone");

 migrationBuilder.AlterColumn<DateTime>(
 name: "CreatedAt",
 table: "ShowroomDailyStaff",
 type: "timestamp without time zone",
 nullable: false,
 oldClrType: typeof(DateTime),
 oldType: "timestamp with time zone");

 migrationBuilder.AlterColumn<DateTime>(
 name: "UpdatedAt",
 table: "ShowroomDailyStaff",
 type: "timestamp without time zone",
 nullable: false,
 oldClrType: typeof(DateTime),
 oldType: "timestamp with time zone");

 migrationBuilder.AlterColumn<DateTime>(
 name: "CreatedAt",
 table: "Showrooms",
 type: "timestamp without time zone",
 nullable: false,
 oldClrType: typeof(DateTime),
 oldType: "timestamp with time zone");

 migrationBuilder.AlterColumn<DateTime>(
 name: "UpdatedAt",
 table: "Showrooms",
 type: "timestamp without time zone",
 nullable: false,
 oldClrType: typeof(DateTime),
 oldType: "timestamp with time zone");
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.AlterColumn<DateTime>(
 name: "AssignmentDate",
 table: "ShowroomDailyStaff",
 type: "timestamp with time zone",
 nullable: false,
 oldClrType: typeof(DateTime),
 oldType: "timestamp without time zone");

 migrationBuilder.AlterColumn<DateTime>(
 name: "CreatedAt",
 table: "ShowroomDailyStaff",
 type: "timestamp with time zone",
 nullable: false,
 oldClrType: typeof(DateTime),
 oldType: "timestamp without time zone");

 migrationBuilder.AlterColumn<DateTime>(
 name: "UpdatedAt",
 table: "ShowroomDailyStaff",
 type: "timestamp with time zone",
 nullable: false,
 oldClrType: typeof(DateTime),
 oldType: "timestamp without time zone");

 migrationBuilder.AlterColumn<DateTime>(
 name: "CreatedAt",
 table: "Showrooms",
 type: "timestamp with time zone",
 nullable: false,
 oldClrType: typeof(DateTime),
 oldType: "timestamp without time zone");

 migrationBuilder.AlterColumn<DateTime>(
 name: "UpdatedAt",
 table: "Showrooms",
 type: "timestamp with time zone",
 nullable: false,
 oldClrType: typeof(DateTime),
 oldType: "timestamp without time zone");
 }
 }
}
