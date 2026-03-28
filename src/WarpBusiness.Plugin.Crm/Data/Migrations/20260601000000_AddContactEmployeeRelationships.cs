using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Plugin.Crm.Data.Migrations
{
    public partial class AddContactEmployeeRelationships : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactEmployeeRelationshipTypes",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactEmployeeRelationshipTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContactEmployeeRelationships",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmployeeEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RelationshipTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactEmployeeRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactEmployeeRelationships_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "crm",
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContactEmployeeRelationships_ContactEmployeeRelationshipTypes_RelationshipTypeId",
                        column: x => x.RelationshipTypeId,
                        principalSchema: "crm",
                        principalTable: "ContactEmployeeRelationshipTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactEmployeeRelationshipTypes_TenantId_Name",
                schema: "crm",
                table: "ContactEmployeeRelationshipTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactEmployeeRelationships_ContactId",
                schema: "crm",
                table: "ContactEmployeeRelationships",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactEmployeeRelationships_RelationshipTypeId",
                schema: "crm",
                table: "ContactEmployeeRelationships",
                column: "RelationshipTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactEmployeeRelationships_ContactId_EmployeeId_RelationshipTypeId",
                schema: "crm",
                table: "ContactEmployeeRelationships",
                columns: new[] { "ContactId", "EmployeeId", "RelationshipTypeId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactEmployeeRelationships",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "ContactEmployeeRelationshipTypes",
                schema: "crm");
        }
    }
}
