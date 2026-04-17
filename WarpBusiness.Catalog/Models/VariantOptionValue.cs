namespace WarpBusiness.Catalog.Models;

public class VariantOptionValue
{
    public Guid VariantId { get; set; }
    public Guid OptionId { get; set; }
    public Guid OptionValueId { get; set; }

    public ProductVariant Variant { get; set; } = null!;
    public ProductOption Option { get; set; } = null!;
    public ProductOptionValue OptionValue { get; set; } = null!;
}
