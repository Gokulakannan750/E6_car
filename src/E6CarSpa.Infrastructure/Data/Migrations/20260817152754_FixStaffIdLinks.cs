using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixStaffIdLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecordedByUsername",
                table: "StaffSalaries",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkerName",
                table: "StaffAdvances",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "RecordedByUsername",
                table: "StaffAdvances",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordedByUsername",
                table: "StaffSalaries");

            migrationBuilder.DropColumn(
                name: "RecordedByUsername",
                table: "StaffAdvances");

            migrationBuilder.AlterColumn<string>(
                name: "WorkerName",
                table: "StaffAdvances",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);
        }
    }
}
