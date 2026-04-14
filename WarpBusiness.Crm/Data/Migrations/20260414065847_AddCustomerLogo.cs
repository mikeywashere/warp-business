using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Crm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Logo",
                schema: "crm",
                table: "Customers",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Logo",
                schema: "crm",
                table: "Customers");
        }
    }
}
