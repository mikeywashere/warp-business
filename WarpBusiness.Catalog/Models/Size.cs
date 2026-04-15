namespace WarpBusiness.Catalog.Models;

public class Size
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Grouping for sizes, e.g. "Clothing", "Shoes", "General"</summary>
    public string SizeType { get; set; } = "General";
    /// <summary>Controls display order within a size type (XS=0, S=1, M=2, L=3, XL=4, etc.)</summary>
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = [];
}
