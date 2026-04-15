namespace WarpBusiness.Catalog.Models;

public class Product
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Brand { get; set; }
    /// <summary>Base/parent SKU — variants may have their own child SKUs</summary>
    public string? SKU { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? ProductTypeId { get; set; }

    public Category? Category { get; set; }
    public ProductType? ProductType { get; set; }
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<ProductMedia> Media { get; set; } = [];
    public ICollection<ProductWarning> Warnings { get; set; } = [];
}
