namespace WarpBusiness.Catalog.Models;

public class ProductVariant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    /// <summary>
    /// Child SKU for this specific variant. When null, the parent product SKU applies.
    /// Must be unique within the tenant when set.
    /// </summary>
    public string? SKU { get; set; }
    /// <summary>Per-variant price adjustment amount; interpretation depends on PriceAdjustmentType.</summary>
    public decimal? Price { get; set; }
    /// <summary>How Price interacts with the parent BasePrice.</summary>
    public PriceAdjustmentType PriceAdjustmentType { get; set; } = PriceAdjustmentType.None;
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Product Product { get; set; } = null!;
    public ICollection<VariantOptionValue> OptionValues { get; set; } = [];
    public ICollection<ProductMedia> Media { get; set; } = [];
}
