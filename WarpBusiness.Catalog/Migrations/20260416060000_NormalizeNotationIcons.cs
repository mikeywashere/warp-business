using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WarpBusiness.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeNotationIcons : Migration
    {
        // Valid enum names from NotationIcon (kept here for self-documentation)
        private const string ValidIconNames =
            "'None','Warning','Info','Note','Caution','Danger','Prohibited'," +
            "'Flammable','Chemical','ElectricalHazard','Recyclable','EcoFriendly'," +
            "'FoodAllergen','Prop65','Compliance','Temperature'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove all legacy/unrecognised icon values — callers can re-select a valid icon.
            migrationBuilder.Sql($"""
                UPDATE catalog."Notations"
                SET "Icon" = NULL
                WHERE "Icon" IS NOT NULL
                  AND "Icon" NOT IN ({ValidIconNames});
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Original free-text values cannot be recovered; Down is intentionally a no-op.
        }
    }
}
