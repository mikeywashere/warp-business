namespace WarpBusiness.Catalog.Models;

/// <summary>
/// A tenant-defined warning label that can be applied to products
/// (e.g., "Contains Nuts", "Contains Dairy", "California Prop 65 Warning").
/// </summary>
public class CatalogWarning
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ProductWarning> ProductWarnings { get; set; } = [];
}
