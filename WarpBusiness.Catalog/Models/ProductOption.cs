namespace WarpBusiness.Catalog.Models;

public class ProductOption
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    /// <summary>True = this option generates variant rows (Color, Size). False = descriptive only (Material, Brand)</summary>
    public bool IsVariantAxis { get; set; } = true;

    public Product Product { get; set; } = null!;
    public ICollection<ProductOptionValue> Values { get; set; } = [];
}
