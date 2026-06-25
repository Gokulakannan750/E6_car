using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProductUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Products");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Unit",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
