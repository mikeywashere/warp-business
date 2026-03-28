namespace WarpBusiness.Plugin.Catalog.Domain;

public class ProductOption
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public ICollection<ProductOptionValue> Values { get; set; } = new List<ProductOptionValue>();
}
