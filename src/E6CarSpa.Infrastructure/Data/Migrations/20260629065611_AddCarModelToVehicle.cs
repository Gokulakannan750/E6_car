using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCarModelToVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarModel",
                table: "Vehicles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarModel",
                table: "Vehicles");
        }
    }
}
