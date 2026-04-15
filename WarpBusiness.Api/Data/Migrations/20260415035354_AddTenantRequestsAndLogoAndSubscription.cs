using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantRequestsAndLogoAndSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnabledFeatures",
                schema: "warp",
                table: "Tenants",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoBase64",
                schema: "warp",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoMimeType",
                schema: "warp",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUsers",
                schema: "warp",
                table: "Tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubscriptionPlan",
                schema: "warp",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantRequests",
                schema: "warp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AssignedToName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "warp",
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantRequests_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalSchema: "warp",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_AssignedToUserId",
                schema: "warp",
                table: "TenantRequests",
                column: "AssignedToUserId",
                filter: "\"AssignedToUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_Status",
                schema: "warp",
                table: "TenantRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenantRequests_TenantId",
                schema: "warp",
                table: "TenantRequests",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantRequests",
                schema: "warp");

            migrationBuilder.DropColumn(
                name: "EnabledFeatures",
                schema: "warp",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LogoBase64",
                schema: "warp",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "LogoMimeType",
                schema: "warp",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MaxUsers",
                schema: "warp",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlan",
                schema: "warp",
                table: "Tenants");
        }
    }
}
