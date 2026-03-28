namespace WarpBusiness.Plugin.Catalog.Domain;

public class VariantOptionValue
{
    public Guid ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public Guid ProductOptionValueId { get; set; }
    public ProductOptionValue? ProductOptionValue { get; set; }
}
