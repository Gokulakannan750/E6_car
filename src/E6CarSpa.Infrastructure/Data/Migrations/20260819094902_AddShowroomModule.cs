using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShowroomModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Showrooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Showrooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShowroomDailyStaff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ShowroomId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CheckIn = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CheckOut = table.Column<TimeSpan>(type: "interval", nullable: true),
                    VehiclesAttended = table.Column<int>(type: "integer", nullable: false),
                    VehiclesCompleted = table.Column<int>(type: "integer", nullable: false),
                    AmountGenerated = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByUsername = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowroomDailyStaff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShowroomDailyStaff_Showrooms_ShowroomId",
                        column: x => x.ShowroomId,
                        principalTable: "Showrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShowroomDailyStaff_Staff_StaffId",
                        column: x => x.StaffId,
                        principalTable: "Staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShowroomDailyStaff_AssignmentDate",
                table: "ShowroomDailyStaff",
                column: "AssignmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_ShowroomDailyStaff_AssignmentDate_StaffId",
                table: "ShowroomDailyStaff",
                columns: new[] { "AssignmentDate", "StaffId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShowroomDailyStaff_ShowroomId",
                table: "ShowroomDailyStaff",
                column: "ShowroomId");

            migrationBuilder.CreateIndex(
                name: "IX_ShowroomDailyStaff_StaffId",
                table: "ShowroomDailyStaff",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Showrooms_Name",
                table: "Showrooms",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShowroomDailyStaff");

            migrationBuilder.DropTable(
                name: "Showrooms");
        }
    }
}
