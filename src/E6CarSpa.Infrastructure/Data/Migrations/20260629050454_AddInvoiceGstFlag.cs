using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceGstFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGstApplicable",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: true);  // existing invoices were GST bills
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGstApplicable",
                table: "Invoices");
        }
    }
}
