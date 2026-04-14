using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Crm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                schema: "crm",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Businesses",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Industry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Businesses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_BusinessId",
                schema: "crm",
                table: "Customers",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_Name_TenantId",
                schema: "crm",
                table: "Businesses",
                columns: new[] { "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_TenantId",
                schema: "crm",
                table: "Businesses",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Businesses_BusinessId",
                schema: "crm",
                table: "Customers",
                column: "BusinessId",
                principalSchema: "crm",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Businesses_BusinessId",
                schema: "crm",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "Businesses",
                schema: "crm");

            migrationBuilder.DropIndex(
                name: "IX_Customers_BusinessId",
                schema: "crm",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                schema: "crm",
                table: "Customers");
        }
    }
}
