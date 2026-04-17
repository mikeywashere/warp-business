namespace WarpBusiness.Catalog.Models;

public class ProductOptionValue
{
    public Guid Id { get; set; }
    public Guid OptionId { get; set; }
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? HexCode { get; set; }
    public int SortOrder { get; set; }

    public ProductOption Option { get; set; } = null!;
    public ICollection<VariantOptionValue> VariantValues { get; set; } = [];
}
