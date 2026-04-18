using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Employees.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPortalFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPortalEnabled",
                schema: "employees",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PortalCanManageAvailability",
                schema: "employees",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PortalCanRequestTimeOff",
                schema: "employees",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PortalCanViewSchedule",
                schema: "employees",
                table: "Employees",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPortalEnabled",
                schema: "employees",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PortalCanManageAvailability",
                schema: "employees",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PortalCanRequestTimeOff",
                schema: "employees",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PortalCanViewSchedule",
                schema: "employees",
                table: "Employees");
        }
    }
}
