using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Taxonomy.Migrations
{
    /// <inheritdoc />
    public partial class InitialTaxonomySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "taxonomy");

            migrationBuilder.CreateTable(
                name: "ExternalTaxonomyCaches",
                schema: "taxonomy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NodeCount = table.Column<int>(type: "integer", nullable: false),
                    SourceVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalTaxonomyCaches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxonomyNodes",
                schema: "taxonomy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    MaterializedPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SourceProvider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourcePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxonomyNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxonomyNodes_TaxonomyNodes_ParentNodeId",
                        column: x => x.ParentNodeId,
                        principalSchema: "taxonomy",
                        principalTable: "TaxonomyNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalTaxonomyNodes",
                schema: "taxonomy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CacheId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParentExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FullPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    IsLeaf = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalTaxonomyNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalTaxonomyNodes_ExternalTaxonomyCaches_CacheId",
                        column: x => x.CacheId,
                        principalSchema: "taxonomy",
                        principalTable: "ExternalTaxonomyCaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaxonomyCaches_Provider",
                schema: "taxonomy",
                table: "ExternalTaxonomyCaches",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaxonomyCaches_Provider_DownloadedAt",
                schema: "taxonomy",
                table: "ExternalTaxonomyCaches",
                columns: new[] { "Provider", "DownloadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaxonomyNodes_CacheId",
                schema: "taxonomy",
                table: "ExternalTaxonomyNodes",
                column: "CacheId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaxonomyNodes_CacheId_ParentExternalId",
                schema: "taxonomy",
                table: "ExternalTaxonomyNodes",
                columns: new[] { "CacheId", "ParentExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalTaxonomyNodes_Provider_ExternalId",
                schema: "taxonomy",
                table: "ExternalTaxonomyNodes",
                columns: new[] { "Provider", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyNodes_ParentNodeId",
                schema: "taxonomy",
                table: "TaxonomyNodes",
                column: "ParentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyNodes_TenantId",
                schema: "taxonomy",
                table: "TaxonomyNodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyNodes_TenantId_MaterializedPath",
                schema: "taxonomy",
                table: "TaxonomyNodes",
                columns: new[] { "TenantId", "MaterializedPath" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyNodes_TenantId_ParentNodeId_Name",
                schema: "taxonomy",
                table: "TaxonomyNodes",
                columns: new[] { "TenantId", "ParentNodeId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxonomyNodes_TenantId_SourceProvider_SourceExternalId",
                schema: "taxonomy",
                table: "TaxonomyNodes",
                columns: new[] { "TenantId", "SourceProvider", "SourceExternalId" },
                unique: true,
                filter: "\"SourceProvider\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalTaxonomyNodes",
                schema: "taxonomy");

            migrationBuilder.DropTable(
                name: "TaxonomyNodes",
                schema: "taxonomy");

            migrationBuilder.DropTable(
                name: "ExternalTaxonomyCaches",
                schema: "taxonomy");
        }
    }
}
