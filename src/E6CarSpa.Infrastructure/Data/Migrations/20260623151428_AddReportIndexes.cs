using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaidAt",
                table: "Payments",
                column: "PaidAt");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreatedAt",
                table: "Invoices",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_PaidAt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CreatedAt",
                table: "Invoices");
        }
    }
}
