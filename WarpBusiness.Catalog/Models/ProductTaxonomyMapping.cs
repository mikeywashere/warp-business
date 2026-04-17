namespace WarpBusiness.Catalog.Models;

public class ProductTaxonomyMapping
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    /// <summary>Provider key string (e.g. "google") — no FK cross-schema</summary>
    public string ProviderKey { get; set; } = string.Empty;
    /// <summary>TaxonomyNode.Id from common_taxonomy schema — raw Guid, no EF navigation</summary>
    public Guid TaxonomyNodeId { get; set; }
    /// <summary>Cached node name for display without cross-schema join</summary>
    public string NodeName { get; set; } = string.Empty;
    /// <summary>Cached full path for display</summary>
    public string NodeFullPath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public Product Product { get; set; } = null!;
    public ICollection<ProductTaxonomyAttributeValue> AttributeValues { get; set; } = [];
}
