using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations;

public partial class AddCustomerNotes : Migration
{
 protected override void Up(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.CreateTable(
 name: "CustomerNotes",
 columns: table => new
 {
 Id = table.Column<Guid>(type: "uuid", nullable: false),
 CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
 Text = table.Column<string>(type: "text", nullable: false),
 CreatedByStaffId = table.Column<Guid>(type: "uuid", nullable: true),
 CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
 UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
 },
 constraints: table =>
 {
 table.PrimaryKey("PK_CustomerNotes", x => x.Id);
 table.ForeignKey(
 name: "FK_CustomerNotes_Customers_CustomerId",
 column: x => x.CustomerId,
 principalTable: "Customers",
 principalColumn: "Id",
 onDelete: ReferentialAction.Cascade);
 });

 migrationBuilder.CreateIndex(
 name: "IX_CustomerNotes_CustomerId",
 table: "CustomerNotes",
 column: "CustomerId");
 }

 protected override void Down(MigrationBuilder migrationBuilder)
 {
 migrationBuilder.DropTable(
 name: "CustomerNotes");
 }
}
