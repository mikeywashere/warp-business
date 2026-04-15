namespace WarpBusiness.Catalog.Models;

/// <summary>
/// The actual attribute value assigned to a specific product variant.
/// Exactly one of AttributeOptionId, TextValue, or NumberValue is populated
/// depending on the attribute type's ValueType.
/// </summary>
public class ProductVariantAttributeValue
{
    public Guid VariantId { get; set; }
    public Guid AttributeTypeId { get; set; }
    /// <summary>Set when AttributeType.ValueType == Select.</summary>
    public Guid? AttributeOptionId { get; set; }
    /// <summary>Set when AttributeType.ValueType == FreeText.</summary>
    public string? TextValue { get; set; }
    /// <summary>Set when AttributeType.ValueType == Number.</summary>
    public decimal? NumberValue { get; set; }

    public ProductVariant Variant { get; set; } = null!;
    public CatalogAttributeType AttributeType { get; set; } = null!;
    public CatalogAttributeOption? AttributeOption { get; set; }
}
