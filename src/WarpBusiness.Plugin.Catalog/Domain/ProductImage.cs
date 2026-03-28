namespace WarpBusiness.Plugin.Catalog.Domain;

public class ProductImage
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public string Url { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? AltText { get; set; }
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }

    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
