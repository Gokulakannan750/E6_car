using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace E6CarSpa.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeAndStaffSalary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Income"" (
                    ""Id"" uuid NOT NULL,
                    ""Source"" character varying(120) NOT NULL,
                    ""Amount"" numeric(12,2) NOT NULL,
                    ""IncomeDate"" timestamp with time zone NOT NULL,
                    ""Note"" character varying(300),
                    ""RecordedByUserId"" uuid,
                    ""DeletedAt"" timestamp with time zone,
                    ""DeletedByUserId"" uuid,
                    ""DeletedByUsername"" text,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone,
                    CONSTRAINT ""PK_Income"" PRIMARY KEY (""Id"")
                );
                CREATE INDEX IF NOT EXISTS ""IX_Income_IncomeDate"" ON ""Income"" (""IncomeDate"");
            ");

            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""StaffSalaries"" (
                    ""Id"" uuid NOT NULL,
                    ""StaffId"" uuid NOT NULL,
                    ""Amount"" numeric(12,2) NOT NULL,
                    ""SalaryDate"" timestamp with time zone NOT NULL,
                    ""Note"" character varying(300),
                    ""RecordedByUserId"" uuid,
                    ""DeletedAt"" timestamp with time zone,
                    ""DeletedByUserId"" uuid,
                    ""DeletedByUsername"" text,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone,
                    CONSTRAINT ""PK_StaffSalaries"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_StaffSalaries_Staff_StaffId"" FOREIGN KEY (""StaffId"") REFERENCES ""Staff"" (""Id"") ON DELETE RESTRICT
                );
                CREATE INDEX IF NOT EXISTS ""IX_StaffSalaries_StaffId_SalaryDate"" ON ""StaffSalaries"" (""StaffId"", ""SalaryDate"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Income");
            migrationBuilder.DropTable(name: "StaffSalaries");
        }
    }
}
