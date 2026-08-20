using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCheckInCheckOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckIn",
                table: "ShowroomDailyStaff");

            migrationBuilder.DropColumn(
                name: "CheckOut",
                table: "ShowroomDailyStaff");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "CheckIn",
                table: "ShowroomDailyStaff",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "CheckOut",
                table: "ShowroomDailyStaff",
                type: "interval",
                nullable: true);
        }
    }
}
