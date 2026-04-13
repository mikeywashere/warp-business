using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Employees.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdUniqueAndTenantScopedEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_Email",
                schema: "employees",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email_TenantId",
                schema: "employees",
                table: "Employees",
                columns: new[] { "Email", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UserId",
                schema: "employees",
                table: "Employees",
                column: "UserId",
                unique: true,
                filter: "\"UserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_Email_TenantId",
                schema: "employees",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_UserId",
                schema: "employees",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Email",
                schema: "employees",
                table: "Employees",
                column: "Email",
                unique: true);
        }
    }
}
