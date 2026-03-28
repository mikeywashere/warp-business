using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Plugin.Catalog.Data.Migrations;

/// <inheritdoc />
public partial class AddCatalogPlugin : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "catalog");

        migrationBuilder.CreateTable(
            name: "Categories",
            schema: "catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ParentCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                ImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Categories", x => x.Id);
                table.ForeignKey(
                    name: "FK_Categories_Categories_ParentCategoryId",
                    column: x => x.ParentCategoryId,
                    principalSchema: "catalog",
                    principalTable: "Categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Products",
            schema: "catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                ShortDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Slug = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Brand = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                ProductType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                BasePrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                CompareAtPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                CostPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                Weight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                WeightUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                Length = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                Width = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                Height = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                DimensionUnit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                IsTaxable = table.Column<bool>(type: "boolean", nullable: false),
                TaxCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                MetaTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                MetaDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Tags = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Products", x => x.Id);
                table.ForeignKey(
                    name: "FK_Products_Categories_CategoryId",
                    column: x => x.CategoryId,
                    principalSchema: "catalog",
                    principalTable: "Categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "ProductOptions",
            schema: "catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductOptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProductOptions_Products_ProductId",
                    column: x => x.ProductId,
                    principalSchema: "catalog",
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProductVariants",
            schema: "catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                Sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Barcode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                CostPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                Weight = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                StockQuantity = table.Column<int>(type: "integer", nullable: false),
                LowStockThreshold = table.Column<int>(type: "integer", nullable: true),
                TrackInventory = table.Column<bool>(type: "boolean", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductVariants", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProductVariants_Products_ProductId",
                    column: x => x.ProductId,
                    principalSchema: "catalog",
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProductImages",
            schema: "catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductVariantId = table.Column<Guid>(type: "uuid", nullable: true),
                Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                AltText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductImages", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProductImages_Products_ProductId",
                    column: x => x.ProductId,
                    principalSchema: "catalog",
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProductImages_ProductVariants_ProductVariantId",
                    column: x => x.ProductVariantId,
                    principalSchema: "catalog",
                    principalTable: "ProductVariants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "ProductIngredients",
            schema: "catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Quantity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                IsAllergen = table.Column<bool>(type: "boolean", nullable: false),
                AllergenType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductIngredients", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProductIngredients_Products_ProductId",
                    column: x => x.ProductId,
                    principalSchema: "catalog",
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProductOptionValues",
            schema: "catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductOptionId = table.Column<Guid>(type: "uuid", nullable: false),
                Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                DisplayOrder = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductOptionValues", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProductOptionValues_ProductOptions_ProductOptionId",
                    column: x => x.ProductOptionId,
                    principalSchema: "catalog",
                    principalTable: "ProductOptions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "VariantOptionValues",
            schema: "catalog",
            columns: table => new
            {
                ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductOptionValueId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VariantOptionValues", x => new { x.ProductVariantId, x.ProductOptionValueId });
                table.ForeignKey(
                    name: "FK_VariantOptionValues_ProductOptionValues_ProductOptionValueId",
                    column: x => x.ProductOptionValueId,
                    principalSchema: "catalog",
                    principalTable: "ProductOptionValues",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_VariantOptionValues_ProductVariants_ProductVariantId",
                    column: x => x.ProductVariantId,
                    principalSchema: "catalog",
                    principalTable: "ProductVariants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Indexes
        migrationBuilder.CreateIndex(name: "IX_Categories_TenantId_Name", schema: "catalog", table: "Categories", columns: new[] { "TenantId", "Name" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Categories_TenantId_Slug", schema: "catalog", table: "Categories", columns: new[] { "TenantId", "Slug" }, unique: true, filter: "\"Slug\" IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_Categories_ParentCategoryId", schema: "catalog", table: "Categories", column: "ParentCategoryId");

        migrationBuilder.CreateIndex(name: "IX_Products_TenantId_Sku", schema: "catalog", table: "Products", columns: new[] { "TenantId", "Sku" }, unique: true, filter: "\"Sku\" IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_Products_TenantId_Slug", schema: "catalog", table: "Products", columns: new[] { "TenantId", "Slug" }, unique: true, filter: "\"Slug\" IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_Products_TenantId_Barcode", schema: "catalog", table: "Products", columns: new[] { "TenantId", "Barcode" }, unique: true, filter: "\"Barcode\" IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_Products_CategoryId", schema: "catalog", table: "Products", column: "CategoryId");

        migrationBuilder.CreateIndex(name: "IX_ProductOptions_ProductId_Name", schema: "catalog", table: "ProductOptions", columns: new[] { "ProductId", "Name" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_ProductOptionValues_ProductOptionId_Value", schema: "catalog", table: "ProductOptionValues", columns: new[] { "ProductOptionId", "Value" }, unique: true);

        migrationBuilder.CreateIndex(name: "IX_ProductVariants_TenantId_Sku", schema: "catalog", table: "ProductVariants", columns: new[] { "TenantId", "Sku" }, unique: true, filter: "\"Sku\" IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_ProductVariants_ProductId", schema: "catalog", table: "ProductVariants", column: "ProductId");

        migrationBuilder.CreateIndex(name: "IX_ProductImages_ProductId", schema: "catalog", table: "ProductImages", column: "ProductId");
        migrationBuilder.CreateIndex(name: "IX_ProductImages_ProductVariantId", schema: "catalog", table: "ProductImages", column: "ProductVariantId");

        migrationBuilder.CreateIndex(name: "IX_ProductIngredients_ProductId_Name", schema: "catalog", table: "ProductIngredients", columns: new[] { "ProductId", "Name" }, unique: true);

        migrationBuilder.CreateIndex(name: "IX_VariantOptionValues_ProductOptionValueId", schema: "catalog", table: "VariantOptionValues", column: "ProductOptionValueId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VariantOptionValues", schema: "catalog");
        migrationBuilder.DropTable(name: "ProductImages", schema: "catalog");
        migrationBuilder.DropTable(name: "ProductIngredients", schema: "catalog");
        migrationBuilder.DropTable(name: "ProductOptionValues", schema: "catalog");
        migrationBuilder.DropTable(name: "ProductVariants", schema: "catalog");
        migrationBuilder.DropTable(name: "ProductOptions", schema: "catalog");
        migrationBuilder.DropTable(name: "Products", schema: "catalog");
        migrationBuilder.DropTable(name: "Categories", schema: "catalog");
    }
}
