namespace WarpBusiness.Catalog.Models;

/// <summary>
/// Junction between a ProductType and the CatalogAttributeTypes it uses.
/// Defines which attributes are required or optional for variants of products in this type.
/// </summary>
public class ProductTypeAttribute
{
    public Guid ProductTypeId { get; set; }
    public Guid AttributeTypeId { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }

    public ProductType ProductType { get; set; } = null!;
    public CatalogAttributeType AttributeType { get; set; } = null!;
}
