namespace WarpBusiness.Catalog.Models;

/// <summary>
/// A tenant-defined notation label that can be applied to products
/// (e.g., "Contains Nuts", "Contains Dairy", "California Prop 65 Warning").
/// </summary>
public class CatalogNotation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Optional icon shown with the notation (e.g., Warning, Info, Prop65).</summary>
    public NotationIcon? Icon { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ProductNotation> ProductNotations { get; set; } = [];
}
