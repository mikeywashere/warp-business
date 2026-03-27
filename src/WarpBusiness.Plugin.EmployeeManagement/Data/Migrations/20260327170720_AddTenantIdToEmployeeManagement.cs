using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Plugin.EmployeeManagement.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToEmployeeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_Email",
                schema: "employees",
                table: "Employees");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "employees",
                table: "Employees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_Email",
                schema: "employees",
                table: "Employees",
                columns: new[] { "TenantId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_TenantId_Email",
                schema: "employees",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TenantId",
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
