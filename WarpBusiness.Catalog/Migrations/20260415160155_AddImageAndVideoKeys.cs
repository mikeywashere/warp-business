using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddImageAndVideoKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VideoKey",
                schema: "catalog",
                table: "ProductVariants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoKey",
                schema: "catalog",
                table: "Products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoKey",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "VideoKey",
                schema: "catalog",
                table: "Products");
        }
    }
}
