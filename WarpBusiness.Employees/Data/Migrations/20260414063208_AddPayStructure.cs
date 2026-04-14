using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Employees.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "employees",
                table: "Employees",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<decimal>(
                name: "PayAmount",
                schema: "employees",
                table: "Employees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PayType",
                schema: "employees",
                table: "Employees",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Salary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "employees",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PayAmount",
                schema: "employees",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PayType",
                schema: "employees",
                table: "Employees");
        }
    }
}
