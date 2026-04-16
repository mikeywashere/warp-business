namespace WarpBusiness.Catalog.Models;

/// <summary>
/// A per-tenant product type template that defines which attribute types apply
/// to a product line (e.g., "Clothing" → Size + Color, "Computer Storage" → Capacity + Dimensions).
/// </summary>
public class ProductType
{
    /// <summary>
    /// The unique identifier for this product type.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The tenant this product type belongs to, used for multi-tenant isolation.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The display name of the product type (e.g., "Clothing", "Computer Storage").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// An optional description providing additional context about the product type.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether this product type is currently active and available for use.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The date and time when this product type was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// The date and time when this product type was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The collection of attribute types associated with this product type (e.g., Size, Color, Capacity).
    /// </summary>
    public ICollection<ProductTypeAttribute> Attributes { get; set; } = [];

    /// <summary>
    /// The collection of products that belong to this product type.
    /// </summary>
    public ICollection<Product> Products { get; set; } = [];
}
