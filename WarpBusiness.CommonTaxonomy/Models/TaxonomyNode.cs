namespace WarpBusiness.CommonTaxonomy.Models;

public class TaxonomyNode
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    /// <summary>Provider-assigned ID (e.g. Google's "166", Amazon's ASIN browse node ID)</summary>
    public string ExternalId { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public int Depth { get; set; }
    public bool IsLeaf { get; set; }

    public TaxonomyProvider Provider { get; set; } = null!;
    public TaxonomyNode? Parent { get; set; }
    public ICollection<TaxonomyNode> Children { get; set; } = [];
    /// <summary>Attributes denormalized on this node (including inherited from ancestors)</summary>
    public ICollection<TaxonomyNodeAttribute> Attributes { get; set; } = [];
}
