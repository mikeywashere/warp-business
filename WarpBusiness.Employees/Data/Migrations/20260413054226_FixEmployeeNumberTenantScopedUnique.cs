using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Employees.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixEmployeeNumberTenantScopedUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_EmployeeNumber",
                schema: "employees",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber_TenantId",
                schema: "employees",
                table: "Employees",
                columns: new[] { "EmployeeNumber", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_EmployeeNumber_TenantId",
                schema: "employees",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                schema: "employees",
                table: "Employees",
                column: "EmployeeNumber",
                unique: true);
        }
    }
}
