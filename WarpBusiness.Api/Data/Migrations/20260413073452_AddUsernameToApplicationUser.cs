using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                schema: "warp",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Username",
                schema: "warp",
                table: "Users");
        }
    }
}
