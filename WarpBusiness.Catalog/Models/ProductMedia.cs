namespace WarpBusiness.Catalog.Models;

public enum MediaType { Image, Video }

public class ProductMedia
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string ObjectKey { get; set; } = string.Empty;
    public MediaType MediaType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Product? Product { get; set; }
    public ProductVariant? Variant { get; set; }
}
