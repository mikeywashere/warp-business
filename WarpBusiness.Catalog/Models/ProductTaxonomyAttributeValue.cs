namespace WarpBusiness.Catalog.Models;

public class ProductTaxonomyAttributeValue
{
    public Guid Id { get; set; }
    public Guid MappingId { get; set; }
    public Guid TenantId { get; set; }
    /// <summary>TaxonomyNodeAttribute.Id — raw Guid, no EF navigation cross-schema</summary>
    public Guid AttributeId { get; set; }
    public string AttributeName { get; set; } = string.Empty;
    public string? TextValue { get; set; }
    public decimal? NumberValue { get; set; }
    public bool? BoolValue { get; set; }

    public ProductTaxonomyMapping Mapping { get; set; } = null!;
}
