using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExtractCrmToPlugin : Migration
    {
        // CRM entities (Contacts, Companies, Deals, Activities, CustomFields) have been
        // extracted to WarpBusiness.Plugin.Crm with its own CrmDbContext using the 'crm' schema.
        // The original public-schema tables are retained for backward compatibility.
        // Fresh databases should use the crm schema exclusively (managed by CrmDbContext migrations).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) { }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
