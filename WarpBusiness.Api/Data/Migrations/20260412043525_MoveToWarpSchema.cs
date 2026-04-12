using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveToWarpSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "warp");

            migrationBuilder.RenameTable(
                name: "UserTenantMemberships",
                newName: "UserTenantMemberships",
                newSchema: "warp");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "Users",
                newSchema: "warp");

            migrationBuilder.RenameTable(
                name: "Tenants",
                newName: "Tenants",
                newSchema: "warp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "UserTenantMemberships",
                schema: "warp",
                newName: "UserTenantMemberships");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "warp",
                newName: "Users");

            migrationBuilder.RenameTable(
                name: "Tenants",
                schema: "warp",
                newName: "Tenants");
        }
    }
}
