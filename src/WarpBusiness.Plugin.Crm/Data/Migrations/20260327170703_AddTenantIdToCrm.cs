using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Plugin.Crm.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToCrm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomFieldDefinitions_EntityType_DisplayOrder",
                schema: "crm",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CustomFieldDefinitions_EntityType_Name",
                schema: "crm",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Companies_Name",
                schema: "crm",
                table: "Companies");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "crm",
                table: "Deals",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "crm",
                table: "CustomFieldDefinitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "crm",
                table: "Contacts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "crm",
                table: "Companies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "crm",
                table: "Activities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_TenantId_EntityType_DisplayOrder",
                schema: "crm",
                table: "CustomFieldDefinitions",
                columns: new[] { "TenantId", "EntityType", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_TenantId_EntityType_Name",
                schema: "crm",
                table: "CustomFieldDefinitions",
                columns: new[] { "TenantId", "EntityType", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_Name",
                schema: "crm",
                table: "Companies",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomFieldDefinitions_TenantId_EntityType_DisplayOrder",
                schema: "crm",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CustomFieldDefinitions_TenantId_EntityType_Name",
                schema: "crm",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Companies_TenantId_Name",
                schema: "crm",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "crm",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "crm",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "crm",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "crm",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "crm",
                table: "Activities");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_EntityType_DisplayOrder",
                schema: "crm",
                table: "CustomFieldDefinitions",
                columns: new[] { "EntityType", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_EntityType_Name",
                schema: "crm",
                table: "CustomFieldDefinitions",
                columns: new[] { "EntityType", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                schema: "crm",
                table: "Companies",
                column: "Name",
                unique: true);
        }
    }
}
