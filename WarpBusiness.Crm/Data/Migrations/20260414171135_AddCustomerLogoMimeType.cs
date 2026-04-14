using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Crm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerLogoMimeType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoMimeType",
                schema: "crm",
                table: "Customers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoMimeType",
                schema: "crm",
                table: "Customers");
        }
    }
}
