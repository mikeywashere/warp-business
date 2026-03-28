namespace WarpBusiness.Plugin.Catalog.Domain;

public class Product
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? Slug { get; set; }

    // Identification
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public string? Brand { get; set; }
    public string? Manufacturer { get; set; }

    // Classification
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public ProductType ProductType { get; set; } = ProductType.General;
    public ProductStatus Status { get; set; } = ProductStatus.Draft;

    // Pricing
    public decimal BasePrice { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public string Currency { get; set; } = "USD";

    // Physical attributes
    public decimal? Weight { get; set; }
    public string? WeightUnit { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public string? DimensionUnit { get; set; }

    // Tax
    public bool IsTaxable { get; set; } = true;
    public string? TaxCode { get; set; }

    // SEO
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    // Flexible tagging
    public string? Tags { get; set; }

    // Audit
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation
    public ICollection<ProductOption> Options { get; set; } = new List<ProductOption>();
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductIngredient> Ingredients { get; set; } = new List<ProductIngredient>();
}

public enum ProductType
{
    General,
    Apparel,
    Food,
    Digital,
    Service
}

public enum ProductStatus
{
    Draft,
    Active,
    Discontinued,
    Archived
}
