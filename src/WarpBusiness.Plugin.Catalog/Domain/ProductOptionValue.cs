namespace WarpBusiness.Plugin.Catalog.Domain;

public class ProductOptionValue
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductOptionId { get; set; }
    public ProductOption? ProductOption { get; set; }
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public ICollection<VariantOptionValue> VariantOptionValues { get; set; } = new List<VariantOptionValue>();
}
