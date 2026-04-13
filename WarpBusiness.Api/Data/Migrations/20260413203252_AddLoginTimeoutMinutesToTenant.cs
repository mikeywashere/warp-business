using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginTimeoutMinutesToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoginTimeoutMinutes",
                schema: "warp",
                table: "Tenants",
                type: "integer",
                nullable: true,
                defaultValue: 480);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoginTimeoutMinutes",
                schema: "warp",
                table: "Tenants");
        }
    }
}
