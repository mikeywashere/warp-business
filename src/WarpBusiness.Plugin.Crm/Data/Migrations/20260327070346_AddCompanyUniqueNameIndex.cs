using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Plugin.Crm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyUniqueNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_Name",
                schema: "crm",
                table: "Companies");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                schema: "crm",
                table: "Companies",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Companies_Name",
                schema: "crm",
                table: "Companies");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                schema: "crm",
                table: "Companies",
                column: "Name");
        }
    }
}
