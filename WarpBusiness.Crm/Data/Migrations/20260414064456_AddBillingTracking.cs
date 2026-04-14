using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Crm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "crm",
                table: "Customers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BillingCurrency",
                schema: "crm",
                table: "CustomerEmployees",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "BillingRate",
                schema: "crm",
                table: "CustomerEmployees",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerEmployees_CustomerId_BillingCurrency",
                schema: "crm",
                table: "CustomerEmployees",
                columns: new[] { "CustomerId", "BillingCurrency" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerEmployees_CustomerId_BillingCurrency",
                schema: "crm",
                table: "CustomerEmployees");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "crm",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BillingCurrency",
                schema: "crm",
                table: "CustomerEmployees");

            migrationBuilder.DropColumn(
                name: "BillingRate",
                schema: "crm",
                table: "CustomerEmployees");
        }
    }
}
