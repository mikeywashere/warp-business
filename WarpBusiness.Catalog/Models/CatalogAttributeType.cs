namespace WarpBusiness.Catalog.Models;

/// <summary>
/// Defines a kind of attribute that can be assigned to product variants
/// (e.g., "Color", "Size", "Capacity", "Weight").
/// Attribute types are defined per tenant and can be reused across product types.
/// </summary>
public class CatalogAttributeType
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AttributeValueType ValueType { get; set; } = AttributeValueType.Select;
    /// <summary>Unit label for Number-type attributes (e.g., "GB", "kg", "cm"). Null for other types.</summary>
    public string? Unit { get; set; }
    /// <summary>When true, Select-type options display a color swatch (hex code field is shown).</summary>
    public bool HasColorPicker { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<CatalogAttributeOption> Options { get; set; } = [];
    public ICollection<ProductTypeAttribute> ProductTypeAttributes { get; set; } = [];
    public ICollection<ProductVariantAttributeValue> VariantValues { get; set; } = [];
}
