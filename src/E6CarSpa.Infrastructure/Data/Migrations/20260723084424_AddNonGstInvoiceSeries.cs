using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNonGstInvoiceSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LastNonGstSequence",
                table: "CompanySettings",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "LastNonGstYear",
                table: "CompanySettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NonGstInvoicePrefix",
                table: "CompanySettings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastNonGstSequence",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "LastNonGstYear",
                table: "CompanySettings");

            migrationBuilder.DropColumn(
                name: "NonGstInvoicePrefix",
                table: "CompanySettings");
        }
    }
}
