namespace WarpBusiness.Catalog.Models;

/// <summary>
/// A per-tenant product type template that defines which attribute types apply
/// to a product line (e.g., "Clothing" → Size + Color, "Computer Storage" → Capacity + Dimensions).
/// </summary>
public class ProductType
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ProductTypeAttribute> Attributes { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}
