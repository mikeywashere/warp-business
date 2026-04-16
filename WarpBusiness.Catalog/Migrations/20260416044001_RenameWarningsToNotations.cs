using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class RenameWarningsToNotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename ProductWarnings FK constraint to ProductNotations FK
            migrationBuilder.DropForeignKey(
                name: "FK_ProductWarnings_Warnings_WarningId",
                schema: "catalog",
                table: "ProductWarnings");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductWarnings_Products_ProductId",
                schema: "catalog",
                table: "ProductWarnings");

            // Drop existing indices before renaming
            migrationBuilder.DropIndex(
                name: "IX_ProductWarnings_WarningId",
                schema: "catalog",
                table: "ProductWarnings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductWarnings",
                schema: "catalog",
                table: "ProductWarnings");

            migrationBuilder.DropIndex(
                name: "IX_Warnings_TenantId",
                schema: "catalog",
                table: "Warnings");

            migrationBuilder.DropIndex(
                name: "IX_Warnings_TenantId_Name",
                schema: "catalog",
                table: "Warnings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Warnings",
                schema: "catalog",
                table: "Warnings");

            // Rename tables
            migrationBuilder.RenameTable(
                name: "Warnings",
                schema: "catalog",
                newName: "Notations",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "ProductWarnings",
                schema: "catalog",
                newName: "ProductNotations",
                newSchema: "catalog");

            // Rename column in ProductNotations
            migrationBuilder.RenameColumn(
                name: "WarningId",
                schema: "catalog",
                table: "ProductNotations",
                newName: "NotationId");

            // Update Icon column to use string enum with max length (was text, now varchar(50))
            migrationBuilder.AlterColumn<string>(
                name: "Icon",
                schema: "catalog",
                table: "Notations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // Re-create primary keys and indices with new names
            migrationBuilder.AddPrimaryKey(
                name: "PK_Notations",
                schema: "catalog",
                table: "Notations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductNotations",
                schema: "catalog",
                table: "ProductNotations",
                columns: new[] { "ProductId", "NotationId" });

            migrationBuilder.CreateIndex(
                name: "IX_Notations_TenantId",
                schema: "catalog",
                table: "Notations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notations_TenantId_Name",
                schema: "catalog",
                table: "Notations",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductNotations_NotationId",
                schema: "catalog",
                table: "ProductNotations",
                column: "NotationId");

            // Re-create foreign keys with new names
            migrationBuilder.AddForeignKey(
                name: "FK_ProductNotations_Notations_NotationId",
                schema: "catalog",
                table: "ProductNotations",
                column: "NotationId",
                principalSchema: "catalog",
                principalTable: "Notations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductNotations_Products_ProductId",
                schema: "catalog",
                table: "ProductNotations",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: drop FKs
            migrationBuilder.DropForeignKey(
                name: "FK_ProductNotations_Notations_NotationId",
                schema: "catalog",
                table: "ProductNotations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductNotations_Products_ProductId",
                schema: "catalog",
                table: "ProductNotations");

            // Drop indices
            migrationBuilder.DropIndex(
                name: "IX_ProductNotations_NotationId",
                schema: "catalog",
                table: "ProductNotations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductNotations",
                schema: "catalog",
                table: "ProductNotations");

            migrationBuilder.DropIndex(
                name: "IX_Notations_TenantId",
                schema: "catalog",
                table: "Notations");

            migrationBuilder.DropIndex(
                name: "IX_Notations_TenantId_Name",
                schema: "catalog",
                table: "Notations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Notations",
                schema: "catalog",
                table: "Notations");

            // Rename tables back
            migrationBuilder.RenameTable(
                name: "Notations",
                schema: "catalog",
                newName: "Warnings",
                newSchema: "catalog");

            migrationBuilder.RenameTable(
                name: "ProductNotations",
                schema: "catalog",
                newName: "ProductWarnings",
                newSchema: "catalog");

            // Rename column back
            migrationBuilder.RenameColumn(
                name: "NotationId",
                schema: "catalog",
                table: "ProductWarnings",
                newName: "WarningId");

            // Revert Icon column to text
            migrationBuilder.AlterColumn<string>(
                name: "Icon",
                schema: "catalog",
                table: "Warnings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            // Re-create PKs and indices with old names
            migrationBuilder.AddPrimaryKey(
                name: "PK_Warnings",
                schema: "catalog",
                table: "Warnings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductWarnings",
                schema: "catalog",
                table: "ProductWarnings",
                columns: new[] { "ProductId", "WarningId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductWarnings_WarningId",
                schema: "catalog",
                table: "ProductWarnings",
                column: "WarningId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_TenantId",
                schema: "catalog",
                table: "Warnings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Warnings_TenantId_Name",
                schema: "catalog",
                table: "Warnings",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            // Re-create FKs with old names
            migrationBuilder.AddForeignKey(
                name: "FK_ProductWarnings_Products_ProductId",
                schema: "catalog",
                table: "ProductWarnings",
                column: "ProductId",
                principalSchema: "catalog",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductWarnings_Warnings_WarningId",
                schema: "catalog",
                table: "ProductWarnings",
                column: "WarningId",
                principalSchema: "catalog",
                principalTable: "Warnings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
