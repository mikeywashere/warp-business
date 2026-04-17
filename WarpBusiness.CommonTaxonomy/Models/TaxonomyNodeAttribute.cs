namespace WarpBusiness.CommonTaxonomy.Models;

public class TaxonomyNodeAttribute
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TaxonomyAttributeValueType ValueType { get; set; }
    public bool IsRequired { get; set; }
    /// <summary>JSON array of allowed values for Select/MultiSelect types</summary>
    public string? AllowedValues { get; set; }
    public string? Unit { get; set; }
    /// <summary>True if this attribute was inherited from a parent node</summary>
    public bool IsInherited { get; set; }

    public TaxonomyNode Node { get; set; } = null!;
}
