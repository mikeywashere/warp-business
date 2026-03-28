using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCompanyImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "CompanyImage",
                table: "Tenants",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyImageContentType",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActiveTenantId",
                table: "RefreshTokens",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyImage",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "CompanyImageContentType",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ActiveTenantId",
                table: "RefreshTokens");
        }
    }
}
