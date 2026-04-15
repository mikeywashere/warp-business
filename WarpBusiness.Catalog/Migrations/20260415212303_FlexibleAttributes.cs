using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class FlexibleAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Colors_ColorId",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Sizes_SizeId",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropTable(
                name: "Colors",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Sizes",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ColorId",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_Color",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_Color_Size",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_Default",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_Size",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_SizeId",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "ColorId",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "SizeId",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductTypeId",
                schema: "catalog",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AttributeTypes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValueType = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    HasColorPicker = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductTypes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warnings",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warnings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttributeOptions",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HexCode = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttributeOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttributeOptions_AttributeTypes_AttributeTypeId",
                        column: x => x.AttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "AttributeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductTypeAttributes",
                schema: "catalog",
                columns: table => new
                {
                    ProductTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTypeAttributes", x => new { x.ProductTypeId, x.AttributeTypeId });
                    table.ForeignKey(
                        name: "FK_ProductTypeAttributes_AttributeTypes_AttributeTypeId",
                        column: x => x.AttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "AttributeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductTypeAttributes_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalSchema: "catalog",
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductWarnings",
                schema: "catalog",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarningId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductWarnings", x => new { x.ProductId, x.WarningId });
                    table.ForeignKey(
                        name: "FK_ProductWarnings_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductWarnings_Warnings_WarningId",
                        column: x => x.WarningId,
                        principalSchema: "catalog",
                        principalTable: "Warnings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VariantAttributeValues",
                schema: "catalog",
                columns: table => new
                {
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributeOptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TextValue = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    NumberValue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariantAttributeValues", x => new { x.VariantId, x.AttributeTypeId });
                    table.ForeignKey(
                        name: "FK_VariantAttributeValues_AttributeOptions_AttributeOptionId",
                        column: x => x.AttributeOptionId,
                        principalSchema: "catalog",
                        principalTable: "AttributeOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VariantAttributeValues_AttributeTypes_AttributeTypeId",
                        column: x => x.AttributeTypeId,
                        principalSchema: "catalog",
                        principalTable: "AttributeTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VariantAttributeValues_ProductVariants_VariantId",
                        column: x => x.VariantId,
                        principalSchema: "catalog",
                        principalTable: "ProductVariants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId",
                schema: "catalog",
                table: "ProductVariants",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_ProductTypeId",
                schema: "catalog",
                table: "Products",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeOptions_AttributeTypeId",
                schema: "catalog",
                table: "AttributeOptions",
                column: "AttributeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeOptions_AttributeTypeId_TenantId_Value",
                schema: "catalog",
                table: "AttributeOptions",
                columns: new[] { "AttributeTypeId", "TenantId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttributeOptions_TenantId",
                schema: "catalog",
                table: "AttributeOptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeTypes_TenantId",
                schema: "catalog",
                table: "AttributeTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AttributeTypes_TenantId_Name",
                schema: "catalog",
                table: "AttributeTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypeAttributes_AttributeTypeId",
                schema: "catalog",
                table: "ProductTypeAttributes",
                column: "AttributeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypes_TenantId",
                schema: "catalog",
                table: "ProductTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductTypes_TenantId_Name",
                schema: "catalog",
                table: "ProductTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductWarnings_WarningId",
                schema: "catalog",
                table: "ProductWarnings",
                column: "WarningId");

            migrationBuilder.CreateIndex(
                name: "IX_VariantAttributeValues_AttributeOptionId",
                schema: "catalog",
                table: "VariantAttributeValues",
                column: "AttributeOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_VariantAttributeValues_AttributeTypeId",
                schema: "catalog",
                table: "VariantAttributeValues",
                column: "AttributeTypeId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Products_ProductTypes_ProductTypeId",
                schema: "catalog",
                table: "Products",
                column: "ProductTypeId",
                principalSchema: "catalog",
                principalTable: "ProductTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_ProductTypes_ProductTypeId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropTable(
                name: "ProductTypeAttributes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductWarnings",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "VariantAttributeValues",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductTypes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Warnings",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "AttributeOptions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "AttributeTypes",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId",
                schema: "catalog",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_Products_ProductTypeId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductTypeId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.AddColumn<Guid>(
                name: "ColorId",
                schema: "catalog",
                table: "ProductVariants",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SizeId",
                schema: "catalog",
                table: "ProductVariants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Colors",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    HexCode = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Colors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sizes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SizeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "General"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sizes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ColorId",
                schema: "catalog",
                table: "ProductVariants",
                column: "ColorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_Color",
                schema: "catalog",
                table: "ProductVariants",
                columns: new[] { "ProductId", "ColorId" },
                unique: true,
                filter: "\"ColorId\" IS NOT NULL AND \"SizeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_Color_Size",
                schema: "catalog",
                table: "ProductVariants",
                columns: new[] { "ProductId", "ColorId", "SizeId" },
                unique: true,
                filter: "\"ColorId\" IS NOT NULL AND \"SizeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_Default",
                schema: "catalog",
                table: "ProductVariants",
                column: "ProductId",
                unique: true,
                filter: "\"ColorId\" IS NULL AND \"SizeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_Size",
                schema: "catalog",
                table: "ProductVariants",
                columns: new[] { "ProductId", "SizeId" },
                unique: true,
                filter: "\"ColorId\" IS NULL AND \"SizeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_SizeId",
                schema: "catalog",
                table: "ProductVariants",
                column: "SizeId");

            migrationBuilder.CreateIndex(
                name: "IX_Colors_Name_TenantId",
                schema: "catalog",
                table: "Colors",
                columns: new[] { "Name", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Colors_TenantId",
                schema: "catalog",
                table: "Colors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Sizes_Name_SizeType_TenantId",
                schema: "catalog",
                table: "Sizes",
                columns: new[] { "Name", "SizeType", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sizes_TenantId",
                schema: "catalog",
                table: "Sizes",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Colors_ColorId",
                schema: "catalog",
                table: "ProductVariants",
                column: "ColorId",
                principalSchema: "catalog",
                principalTable: "Colors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Sizes_SizeId",
                schema: "catalog",
                table: "ProductVariants",
                column: "SizeId",
                principalSchema: "catalog",
                principalTable: "Sizes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
