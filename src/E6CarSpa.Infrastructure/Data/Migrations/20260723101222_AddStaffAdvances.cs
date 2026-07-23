using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffAdvances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffAdvances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    AdvanceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffAdvances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffAdvances_AdvanceDate",
                table: "StaffAdvances",
                column: "AdvanceDate");

            migrationBuilder.CreateIndex(
                name: "IX_StaffAdvances_WorkerName",
                table: "StaffAdvances",
                column: "WorkerName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffAdvances");
        }
    }
}
