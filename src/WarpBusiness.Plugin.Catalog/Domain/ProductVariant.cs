namespace WarpBusiness.Plugin.Catalog.Domain;

public class ProductVariant
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public string? Sku { get; set; }
    public string? Barcode { get; set; }

    // Price overrides (null = use product base price)
    public decimal? Price { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? Weight { get; set; }

    // Inventory
    public int StockQuantity { get; set; }
    public int? LowStockThreshold { get; set; }
    public bool TrackInventory { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<VariantOptionValue> OptionValues { get; set; } = new List<VariantOptionValue>();
}
