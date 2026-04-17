using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.CommonTaxonomy.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "common_taxonomy");

            migrationBuilder.CreateTable(
                name: "Providers",
                schema: "common_taxonomy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SupportsApiDownload = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsFileImport = table.Column<bool>(type: "boolean", nullable: false),
                    LastDownloadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastDownloadChecksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nodes",
                schema: "common_taxonomy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FullPath = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Depth = table.Column<int>(type: "integer", nullable: false),
                    IsLeaf = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nodes_Nodes_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "common_taxonomy",
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Nodes_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "common_taxonomy",
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NodeAttributes",
                schema: "common_taxonomy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValueType = table.Column<string>(type: "text", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedValues = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsInherited = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NodeAttributes_Nodes_NodeId",
                        column: x => x.NodeId,
                        principalSchema: "common_taxonomy",
                        principalTable: "Nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NodeAttributes_NodeId",
                schema: "common_taxonomy",
                table: "NodeAttributes",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_ParentId",
                schema: "common_taxonomy",
                table: "Nodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_ProviderId",
                schema: "common_taxonomy",
                table: "Nodes",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Nodes_ProviderId_ExternalId",
                schema: "common_taxonomy",
                table: "Nodes",
                columns: new[] { "ProviderId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Providers_Key",
                schema: "common_taxonomy",
                table: "Providers",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NodeAttributes",
                schema: "common_taxonomy");

            migrationBuilder.DropTable(
                name: "Nodes",
                schema: "common_taxonomy");

            migrationBuilder.DropTable(
                name: "Providers",
                schema: "common_taxonomy");
        }
    }
}
