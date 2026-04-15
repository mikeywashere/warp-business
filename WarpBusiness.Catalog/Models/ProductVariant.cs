namespace WarpBusiness.Catalog.Models;

public class ProductVariant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ColorId { get; set; }
    public Guid? SizeId { get; set; }
    /// <summary>
    /// Child SKU for this specific variant. When null, the parent product SKU applies.
    /// Must be unique within the tenant when set.
    /// </summary>
    public string? SKU { get; set; }
    /// <summary>Per-variant price override; when null the product BasePrice applies.</summary>
    public decimal? Price { get; set; }
    public int StockQuantity { get; set; }
    public string? ImageKey { get; set; }
    public string? VideoKey { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Product Product { get; set; } = null!;
    public Color? Color { get; set; }
    public Size? Size { get; set; }
}
