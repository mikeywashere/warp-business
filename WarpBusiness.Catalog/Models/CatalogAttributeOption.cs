namespace WarpBusiness.Catalog.Models;

/// <summary>
/// A predefined value for a Select-type attribute
/// (e.g., "Red" for Color, "XL" for Size, "256 GB" for Capacity).
/// </summary>
public class CatalogAttributeOption
{
    public Guid Id { get; set; }
    public Guid AttributeTypeId { get; set; }
    public Guid TenantId { get; set; }
    public string Value { get; set; } = string.Empty;
    /// <summary>Hex color code for options belonging to a HasColorPicker attribute (e.g., "#FF0000").</summary>
    public string? HexCode { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public CatalogAttributeType AttributeType { get; set; } = null!;
    public ICollection<ProductVariantAttributeValue> VariantValues { get; set; } = [];
}
