using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredCurrencyCode",
                schema: "warp",
                table: "Tenants",
                type: "character(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "warp",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    NumericCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    MinorUnit = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_PreferredCurrencyCode",
                schema: "warp",
                table: "Tenants",
                column: "PreferredCurrencyCode");

            migrationBuilder.AddForeignKey(
                name: "FK_Tenants_Currencies_PreferredCurrencyCode",
                schema: "warp",
                table: "Tenants",
                column: "PreferredCurrencyCode",
                principalSchema: "warp",
                principalTable: "Currencies",
                principalColumn: "Code",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tenants_Currencies_PreferredCurrencyCode",
                schema: "warp",
                table: "Tenants");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "warp");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_PreferredCurrencyCode",
                schema: "warp",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PreferredCurrencyCode",
                schema: "warp",
                table: "Tenants");
        }
    }
}
