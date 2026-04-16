using FluentAssertions;
using WarpBusiness.Catalog.Models;

namespace WarpBusiness.Api.Tests.Catalog;

/// <summary>
/// Tests for <see cref="NotationIconParser"/> — the safe string-to-enum converter used
/// by <c>CatalogDbContext</c> to read the <c>Icon</c> column without throwing on stale data.
///
/// Regression guard for:
///   System.InvalidOperationException: Cannot convert string value '⚠' from the
///   database to any value in the mapped 'NotationIcon' enum.
/// </summary>
public class NotationIconParserTests
{
    [Theory]
    [InlineData("Warning",          NotationIcon.Warning)]
    [InlineData("Info",             NotationIcon.Info)]
    [InlineData("Note",             NotationIcon.Note)]
    [InlineData("Caution",          NotationIcon.Caution)]
    [InlineData("Danger",           NotationIcon.Danger)]
    [InlineData("Prohibited",       NotationIcon.Prohibited)]
    [InlineData("Flammable",        NotationIcon.Flammable)]
    [InlineData("Chemical",         NotationIcon.Chemical)]
    [InlineData("ElectricalHazard", NotationIcon.ElectricalHazard)]
    [InlineData("Recyclable",       NotationIcon.Recyclable)]
    [InlineData("EcoFriendly",      NotationIcon.EcoFriendly)]
    [InlineData("FoodAllergen",     NotationIcon.FoodAllergen)]
    [InlineData("Prop65",           NotationIcon.Prop65)]
    [InlineData("Compliance",       NotationIcon.Compliance)]
    [InlineData("Temperature",      NotationIcon.Temperature)]
    [InlineData("None",             NotationIcon.None)]
    public void ParseOrNull_ValidEnumName_ReturnsCorrectValue(string stored, NotationIcon expected)
    {
        NotationIconParser.ParseOrNull(stored).Should().Be(expected);
    }

    [Theory]
    [InlineData("⚠")]                              // warning emoji — the actual bug trigger
    [InlineData("⚠️")]                             // warning emoji with VS16 selector
    [InlineData("ℹ")]                               // info emoji
    [InlineData("ℹ️")]                              // info emoji with VS16 selector
    [InlineData("📝")]                              // memo emoji
    [InlineData("☠")]                               // skull-and-crossbones
    [InlineData("warning")]                         // lowercase (parser is case-sensitive)
    [InlineData("DANGER")]                          // uppercase
    [InlineData("bi-exclamation-triangle-fill")]    // Bootstrap Icons CSS class
    [InlineData("unknown_icon")]
    [InlineData("42")]
    public void ParseOrNull_UnrecognisedValue_ReturnsNullWithoutThrowing(string legacyValue)
    {
        var act = () => NotationIconParser.ParseOrNull(legacyValue);

        act.Should().NotThrow(
            "unknown icon values must be silently treated as null, " +
            "not throw InvalidOperationException");
        act().Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseOrNull_NullOrEmpty_ReturnsNull(string? value)
    {
        NotationIconParser.ParseOrNull(value).Should().BeNull();
    }

    [Fact]
    public void ParseOrNull_WhitespaceOnly_ReturnsNull()
    {
        // Whitespace is not caught by IsNullOrEmpty, but TryParse also returns false for it.
        NotationIconParser.ParseOrNull("   ").Should().BeNull();
    }
}
