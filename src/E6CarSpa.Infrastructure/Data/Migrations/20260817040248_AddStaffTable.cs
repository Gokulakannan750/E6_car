using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffAdvances_WorkerName",
                table: "StaffAdvances");

            migrationBuilder.AlterColumn<string>(
                name: "WorkerName",
                table: "StaffAdvances",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<Guid>(
                name: "StaffId",
                table: "StaffAdvances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAdvances_StaffId",
                table: "StaffAdvances",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_FullName",
                table: "Staff",
                column: "FullName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffAdvances_Staff_StaffId",
                table: "StaffAdvances",
                column: "StaffId",
                principalTable: "Staff",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ----- Backfill Staff from distinct existing WorkerName values -----
            migrationBuilder.Sql(@"
                INSERT INTO ""Staff"" (""Id"", ""FullName"", ""IsActive"", ""CreatedAt"", ""UpdatedAt"")
                SELECT gen_random_uuid(), TRIM(""WorkerName""), true, NOW(), NOW()
                FROM (SELECT DISTINCT TRIM(""WorkerName"") AS ""WorkerName"" FROM ""StaffAdvances"") d
                WHERE TRIM(""WorkerName"") <> ''
                ON CONFLICT (""FullName"") DO NOTHING;
            ");

            migrationBuilder.Sql(@"
                UPDATE ""StaffAdvances"" sa
                SET ""StaffId"" = s.""Id""
                FROM ""Staff"" s
                WHERE TRIM(sa.""WorkerName"") = TRIM(s.""FullName"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StaffAdvances_Staff_StaffId",
                table: "StaffAdvances");

            migrationBuilder.DropTable(
                name: "Staff");

            migrationBuilder.DropIndex(
                name: "IX_StaffAdvances_StaffId",
                table: "StaffAdvances");

            migrationBuilder.DropColumn(
                name: "StaffId",
                table: "StaffAdvances");

            migrationBuilder.AlterColumn<string>(
                name: "WorkerName",
                table: "StaffAdvances",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAdvances_WorkerName",
                table: "StaffAdvances",
                column: "WorkerName");
        }
    }
}
