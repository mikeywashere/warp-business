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
            // Map legacy emoji values stored before the NotationIcon enum was introduced.
            migrationBuilder.Sql("""
                UPDATE catalog."Notations"
                SET "Icon" = 'Warning'
                WHERE "Icon" IN ('⚠', '⚠️', '!', 'warning', 'Warning');
                """);

            migrationBuilder.Sql("""
                UPDATE catalog."Notations"
                SET "Icon" = 'Info'
                WHERE "Icon" IN ('ℹ', 'ℹ️', 'info', 'Info');
                """);

            migrationBuilder.Sql("""
                UPDATE catalog."Notations"
                SET "Icon" = 'Note'
                WHERE "Icon" IN ('📝', 'note', 'Note');
                """);

            migrationBuilder.Sql("""
                UPDATE catalog."Notations"
                SET "Icon" = 'Caution'
                WHERE "Icon" IN ('⚡', 'caution', 'Caution');
                """);

            migrationBuilder.Sql("""
                UPDATE catalog."Notations"
                SET "Icon" = 'Danger'
                WHERE "Icon" IN ('☠', '☠️', 'danger', 'Danger');
                """);

            // NULL out any remaining unrecognised values so EF's converter doesn't throw.
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
